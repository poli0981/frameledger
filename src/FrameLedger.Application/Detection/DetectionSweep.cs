using FrameLedger.Application.Persistence;
using FrameLedger.Application.Watch;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Application.Detection;

/// <summary>
/// The static detection engine's first production caller (P4 PR-1): once per sweep, every game in the library
/// whose <c>DetectionCacheKey</c> is stale — never scanned, the exe changed, or the rules moved on — is run through
/// <see cref="StaticGameDetector"/> and the result persisted through <see cref="IGameRepository.ApplyDetectionAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>What "stale" is</b> (<c>05_DETECTION</c> §Caching): the stored <c>detection_rules_version</c> differs from the
/// rules file's, or the stored exe size / mtime (schema 0004 — the detection key's own columns, never the consent
/// fingerprint) differ from the file on disk right now. An unreadable exe is skipped and named once per sweep; an
/// unreadable rules file ends the sweep with nothing written.
/// </para>
/// <para>
/// <b>Where it runs.</b> <c>GameFileProbe</c> does blocking file I/O (a bounded directory walk and an 8 MB strings
/// pass), so the sweep belongs on its own background task — the Agent's <c>DetectionHostedService</c> — and never
/// on the orchestrator's poll or a UI thread. It writes only the detected columns of <c>games</c>, under the
/// repository's provenance rule; it is not a third writer of <c>ledger.db</c> (HANDOFF §P4).
/// </para>
/// <para>
/// <b>When it wakes.</b> On an interval, and immediately after <c>UpdateRules</c> re-seeds the rules file
/// (<see cref="RequestNow"/>): a rules update is the trigger §Caching names, and a game the App just added is
/// picked up on the next tick because the App writes the row and the Agent reads the same table.
/// </para>
/// </remarks>
public sealed class DetectionSweep : IDisposable
{
    private readonly IGameRepository _games;
    private readonly IDetectionRulesSource _rules;
    private readonly StaticGameDetector _detector;
    private readonly IExecutableIdentitySource _identity;
    private readonly Action<string> _log;
    private readonly SemaphoreSlim _wake = new(0, 1);

    public DetectionSweep(IGameRepository games, IDetectionRulesSource rules, IGameFileProbe probe, IExecutableIdentitySource identity, Action<string> log)
    {
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        ArgumentNullException.ThrowIfNull(probe);
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _detector = new StaticGameDetector(rules, probe);
    }

    /// <summary>Wake the hosted loop before its interval elapses (the <c>UpdateRules</c> trigger). Idempotent.</summary>
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

    /// <summary>Wait for <see cref="RequestNow"/> or <paramref name="interval"/>, whichever first. True when woken early.</summary>
    public Task<bool> WaitAsync(TimeSpan interval, CancellationToken ct) => _wake.WaitAsync(interval, ct);

    public void Dispose() => _wake.Dispose();

    /// <summary>One pass over the library. Returns what it scanned and what it skipped.</summary>
    public async ValueTask<DetectionSweepReport> SweepOnceAsync(CancellationToken ct = default)
    {
        DetectionRuleSet rules;
        try
        {
            rules = await _rules.LoadAsync(ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _log($"detect: rules unusable, nothing scanned — {ex.Message}");
            return new DetectionSweepReport { RulesUnusable = true };
        }

        int scanned = 0;
        int current = 0;
        int unreadable = 0;
        foreach (GameRow game in await _games.ListAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            if (_identity.Read(game.Fingerprint.ExePath) is not { } onDisk)
            {
                unreadable++;
                _log($"detect: {game.Name} — executable unreadable, skipped ({game.Fingerprint.ExePath})");
                continue;
            }

            if (!IsStale(game, onDisk, rules.RulesVersion))
            {
                current++;
                continue;
            }

            StaticDetectionResult r = await _detector.DetectAsync(game.Fingerprint.ExePath, ct).ConfigureAwait(false);
            var write = new DetectionWrite
            {
                EngineId = r.EngineId,
                EngineVersion = r.EngineVersion,
                PlatformId = r.PlatformId,
                CapabilityIds = r.CapabilityIds,
                RulesVersion = r.RulesVersion,
                ExeSizeBytes = onDisk.SizeBytes,
                ExeMtimeMs = onDisk.MtimeUnixMs,
            };
            if (await _games.ApplyDetectionAsync(game.Id, write, ct).ConfigureAwait(false))
            {
                scanned++;
                _log($"detect: {game.Name} — engine={r.EngineId ?? (r.EngineUndetermined ? "undetermined" : "none")}"
                     + $"{(r.EngineVersion is null ? string.Empty : " " + r.EngineVersion)} platform={r.PlatformId ?? (r.PlatformUndetermined ? "undetermined" : "none")}"
                     + $" capabilities=[{string.Join(",", r.CapabilityIds)}] rules={r.RulesVersion}");
            }
        }

        return new DetectionSweepReport { Scanned = scanned, Current = current, Unreadable = unreadable };
    }

    /// <summary><c>DetectionCacheKey</c> compared field by field against the row: any difference is a re-run.</summary>
    public static bool IsStale(GameRow game, ExecutableFingerprint onDisk, string rulesVersion)
    {
        ArgumentNullException.ThrowIfNull(game);
        return !string.Equals(game.DetectionRulesVersion, rulesVersion, StringComparison.Ordinal)
            || game.DetectionExeSizeBytes != onDisk.SizeBytes
            || game.DetectionExeMtimeMs != onDisk.MtimeUnixMs;
    }
}
