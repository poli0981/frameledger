using FrameLedger.Application.Consent;
using FrameLedger.Application.Detection;
using FrameLedger.Application.Ipc;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Watch;
using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Consent;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.AntiCheat;

/// <summary>
/// The Agent's pre-scan of the library (beta.8, owner decision 2026-09-25): every entry is asked the guard's advisory
/// question — checks 3 and 4 over its executable (<see cref="IAntiCheatGuard.PreScanGameAsync"/>) — once, and again
/// whenever the rules or the executable change, so a game that ships anti-cheat is known before it is ever launched
/// and its hooking is off before anyone could turn it on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists.</b> Until 2026-09-25 the pre-scan ran only when the user asked to turn hooking on
/// (<c>SetHookEnabled</c>), and a session's own checks ran only for a game whose hooking was already on — so a game
/// never enabled stayed <c>hook_prescan_state = 'not_run'</c>, and the App could not tell an anti-cheat game from any
/// other. The owner asked for the hooking part of anti-cheat games to be hidden; that needs to know which they are.
/// </para>
/// <para>
/// <b>What it writes</b> is what the guard answered, through the consent store (the hook-state writer): a finding
/// about the game is the same block every other moment writes (<c>19_SAFETY</c> §What a finding does to the game) —
/// hooking off, the reason on the row — plus the key it was scanned under; a pass is <c>'clean'</c>; a scan that could
/// not answer is <c>'unverified'</c>. A blocked row is never scanned again and never written: nothing clears a block,
/// and a later "clean" or "could not verify" must not read as one.
/// </para>
/// <para>
/// <b>What it does not do.</b> It gates nothing: the chokepoint runs checks 1–4 against the live process at every
/// session start whatever this sweep wrote, exactly as it did before. It never opens a game's process — the scan
/// reads the executable's path, its install folder and the store's files.
/// </para>
/// </remarks>
public sealed class AntiCheatPreScanSweep : IDisposable
{
    private readonly IGameRepository _games;
    private readonly IGameConsentStore _consent;
    private readonly IAntiCheatGuard _guard;
    private readonly IDetectionRulesSource _rules;
    private readonly IExecutableIdentitySource _identity;
    private readonly IIpcEventPublisher? _pipe;
    private readonly Func<long, bool> _isRecording;
    private readonly Action<string> _log;
    private readonly SemaphoreSlim _wake = new(0, 1);

    public AntiCheatPreScanSweep(IGameRepository games, IGameConsentStore consent, IAntiCheatGuard guard, IDetectionRulesSource rules,
        IExecutableIdentitySource identity, Action<string> log, IIpcEventPublisher? pipe = null, Func<long, bool>? isRecording = null)
    {
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _consent = consent ?? throw new ArgumentNullException(nameof(consent));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _pipe = pipe;
        _isRecording = isRecording ?? (static _ => false);
    }

    /// <summary>Wake the hosted loop before its interval elapses (a rules update). Idempotent.</summary>
    public void RequestNow()
    {
        try
        {
            _wake.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already requested and not yet consumed: one wake is one wake.
        }
    }

    /// <summary>Wait for <see cref="RequestNow"/> or <paramref name="interval"/>, whichever first.</summary>
    public Task<bool> WaitAsync(TimeSpan interval, CancellationToken ct) => _wake.WaitAsync(interval, ct);

    public void Dispose() => _wake.Dispose();

    /// <summary>One pass over the library.</summary>
    public async ValueTask<AntiCheatPreScanReport> SweepOnceAsync(CancellationToken ct = default)
    {
        string rulesVersion;
        try
        {
            rulesVersion = (await _rules.LoadAsync(ct).ConfigureAwait(false)).RulesVersion;
        }
        catch (InvalidOperationException ex)
        {
            _log($"prescan: rules unusable, nothing scanned — {ex.Message}");
            return new AntiCheatPreScanReport { RulesUnusable = true };
        }

        var report = new AntiCheatPreScanReport();
        foreach (GameRow game in await _games.ListAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            if (game.BlockedByGuard)
            {
                // A block is final: nothing scans it again, and nothing this sweep writes could read as clearing it.
                report = report with { Blocked = report.Blocked + 1 };
                continue;
            }

            if (_identity.Read(game.Fingerprint.ExePath) is not { } onDisk)
            {
                report = report with { Unreadable = report.Unreadable + 1 };
                continue;    // the detection sweep names it; a file we cannot read is not one we can scan
            }

            if (!IsStale(game, onDisk, rulesVersion) || _isRecording(game.Id))
            {
                report = report with { Current = report.Current + 1 };
                continue;
            }

            report = await ScanAsync(game, onDisk, rulesVersion, report, ct).ConfigureAwait(false);
        }

        return report;
    }

    private async ValueTask<AntiCheatPreScanReport> ScanAsync(GameRow game, ExecutableFingerprint onDisk, string rulesVersion,
        AntiCheatPreScanReport report, CancellationToken ct)
    {
        AntiCheatVerdict verdict = await _guard.PreScanGameAsync(onDisk.ExePath, ct).ConfigureAwait(false);
        if (await _consent.RecordPreScanAsync(onDisk, verdict, rulesVersion, ct).ConfigureAwait(false) != ConsentWriteOutcome.Written)
        {
            return report;
        }

        if (verdict.IsFindingAboutTheGame)
        {
            _log($"prescan: {game.Name} — {verdict.Reason} {verdict.Family} ({verdict.Signal}); hooking off"
                 + (game.HookEnabled ? " (it was on)" : string.Empty));
            if (game.HookEnabled)
            {
                // The one case worth a notice: the user had turned hooking on, and a rules update or a game update has
                // just turned it off. A game whose hooking was already off changes nothing the user can see but its page.
                _pipe?.Publish(IpcMessageType.HookingTurnedOff,
                    new HookingTurnedOffEvent(game.Id, game.Name, verdict.Reason.ToString(), NullIfEmpty(verdict.Family), NullIfEmpty(verdict.Signal)));
            }

            return report with { Found = report.Found + 1 };
        }

        return verdict.IsAllowed
            ? report with { Clean = report.Clean + 1 }
            : report with { Unverified = report.Unverified + 1 };
    }

    /// <summary>The pre-scan's own key (schema 0010) against the rules and the file right now: any difference is a re-scan.</summary>
    public static bool IsStale(GameRow game, ExecutableFingerprint onDisk, string rulesVersion)
    {
        ArgumentNullException.ThrowIfNull(game);
        return !string.Equals(game.HookPrescanRulesVersion, rulesVersion, StringComparison.Ordinal)
            || game.HookPrescanExeSizeBytes != onDisk.SizeBytes
            || game.HookPrescanExeMtimeMs != onDisk.MtimeUnixMs;
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}
