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
    private readonly ExecutableRelocator? _relocator;
    private readonly SemaphoreSlim _wake = new(0, 1);

    // The path each unreadable entry was last reported at (2026-09-23): said once until it changes or reads again. The
    // owner's log repeated the same "executable unreadable" line for the same entries every 15 s.
    private readonly Dictionary<long, string> _unreadableSaid = [];

    /// <summary>The sweep over the library, with the detector it runs and the identity it reads executables with.</summary>
    /// <param name="games">The library.</param>
    /// <param name="rules">The detection rules the detector runs.</param>
    /// <param name="probe">The file probe the detector runs over.</param>
    /// <param name="identity">Reads each row's executable (its fingerprint), or null when it cannot.</param>
    /// <param name="log">The Agent's log line.</param>
    /// <param name="relocator">Follows an executable to a drive that changed its letter (2026-09-22); null sweeps without looking.</param>
    public DetectionSweep(IGameRepository games, IDetectionRulesSource rules, IGameFileProbe probe, IExecutableIdentitySource identity, Action<string> log,
        ExecutableRelocator? relocator = null)
    {
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        ArgumentNullException.ThrowIfNull(probe);
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _relocator = relocator;
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
        int relocated = 0;
        foreach (GameRow listed in await _games.ListAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            (GameRow game, ExecutableFingerprint? read, bool moved, bool gone) = await ReadOrRelocateAsync(listed, ct).ConfigureAwait(false);
            if (moved)
            {
                relocated++;
            }

            if (gone)
            {
                // Merged into its twin this pass (2026-09-23): not an entry any more, so not an unreadable one.
                _unreadableSaid.Remove(game.Id);
                continue;
            }

            if (read is not { } onDisk)
            {
                unreadable++;
                if (!_unreadableSaid.TryGetValue(game.Id, out string? said) || !string.Equals(said, game.Fingerprint.ExePath, StringComparison.OrdinalIgnoreCase))
                {
                    _unreadableSaid[game.Id] = game.Fingerprint.ExePath;
                    _log($"detect: {game.Name} — executable unreadable, skipped ({game.Fingerprint.ExePath}); said once until that changes");
                }

                continue;
            }

            _unreadableSaid.Remove(game.Id);

            if (!IsStale(game, onDisk, rules.RulesVersion))
            {
                current++;
                continue;
            }

            if (await ScanAsync(game, onDisk, ct).ConfigureAwait(false))
            {
                scanned++;
            }
        }

        return new DetectionSweepReport { Scanned = scanned, Current = current, Unreadable = unreadable, Relocated = relocated };
    }

    /// <summary>
    /// What <c>hook_autodisabled_reason</c> says when the sweep turns off hooking on an executable that cannot run as x64
    /// (beta.8). The page says it in the user's language from <c>exe_machine</c>; this is the row's and the log's record.
    /// </summary>
    public static string NotX64Reason(string architecture) => $"executable is {architecture}: the hook runs in x64 processes only";

    /// <summary>One game through the detector and into the row; true when the row took the write.</summary>
    private async ValueTask<bool> ScanAsync(GameRow game, ExecutableFingerprint onDisk, CancellationToken ct)
    {
        StaticDetectionResult r = await _detector.DetectAsync(game.Fingerprint.ExePath, ct).ConfigureAwait(false);
        // The Vulkan fact rides in capability_flags under its own id (P4 PR-2): not a rule id, stored beside them
        // so one column says what the game ships AND whether the layer is its capture side.
        IReadOnlyList<string> capabilities = r.UsesVulkan == true
            ? [.. r.CapabilityIds, StaticDetectionResult.VulkanCapabilityId]
            : r.CapabilityIds;
        var write = new DetectionWrite
        {
            EngineId = r.EngineId,
            EngineVersion = r.EngineVersion,
            PlatformId = r.PlatformId,
            CapabilityIds = capabilities,
            RulesVersion = r.RulesVersion,
            ExeSizeBytes = onDisk.SizeBytes,
            ExeMtimeMs = onDisk.MtimeUnixMs,
            ExeArchitecture = r.ExeArchitecture,
            ExeFileVersion = r.ExeFileVersion,
            ExeProductVersion = r.ExeProductVersion,
            Libraries = r.Libraries,
        };
        if (!await _games.ApplyDetectionAsync(game.Id, write, ct).ConfigureAwait(false))
        {
            return false;
        }

        // A hooking switch that can never work (beta.8): an x86, ARM or 32-bit-preferring executable is a process the x64
        // hook cannot enter. Enabling it is refused up front since this build; a row enabled before it is turned off here
        // rather than refused at every launch. The consent stays: the same row with a 64-bit executable can be re-enabled.
        if (game.HookEnabled && ExecutableArchitecture.IsKnownNotHookable(r.ExeArchitecture))
        {
            string reason = NotX64Reason(r.ExeArchitecture);
            _ = await _games.AutoDisableHookAsync(game.Id, reason, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            _log($"detect: {game.Name} — {reason}; hooking turned off");
        }

        _log($"detect: {game.Name} — engine={r.EngineId ?? (r.EngineUndetermined ? "undetermined" : "none")}"
             + $"{(r.EngineVersion is null ? string.Empty : " " + r.EngineVersion)} platform={r.PlatformId ?? (r.PlatformUndetermined ? "undetermined" : "none")}"
             + $" capabilities=[{string.Join(",", capabilities)}] vulkan={(r.UsesVulkan is { } v ? (v ? "yes" : "no") : "unknown")} rules={r.RulesVersion}"
             + $" arch={r.ExeArchitecture} libraries={r.Libraries.Count}");
        return true;
    }

    /// <summary>
    /// The row's executable as read from disk — and, when it is missing, one more question before the row is skipped
    /// (2026-09-22): is the same file under another drive letter? When it is, the row follows it and this very pass
    /// scans it there. Since 2026-09-23 the relocator can also merge the row into the entry that already holds that
    /// file; the row is then gone, which this says rather than calling it unreadable.
    /// </summary>
    private async ValueTask<(GameRow Game, ExecutableFingerprint? Read, bool Moved, bool Gone)> ReadOrRelocateAsync(GameRow game, CancellationToken ct)
    {
        ExecutableFingerprint? read = _identity.Read(game.Fingerprint.ExePath);
        if (read is not null || _relocator is null)
        {
            return (game, read, false, false);
        }

        if (await _relocator.TryRelocateAsync(game, ct).ConfigureAwait(false) is { } moved)
        {
            return (game with { Fingerprint = moved }, _identity.Read(moved.ExePath), true, false);
        }

        bool gone = await _games.FindByIdAsync(game.Id, ct).ConfigureAwait(false) is null;
        return (game, null, gone, gone);
    }

    /// <summary>
    /// <c>DetectionCacheKey</c> compared field by field against the row: any difference is a re-run — and a row this build
    /// has not read the executable's facts for (schema 0011, <c>exe_machine</c> NULL) is one too, once.
    /// </summary>
    public static bool IsStale(GameRow game, ExecutableFingerprint onDisk, string rulesVersion)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.ExeMachine is null
            || !string.Equals(game.DetectionRulesVersion, rulesVersion, StringComparison.Ordinal)
            || game.DetectionExeSizeBytes != onDisk.SizeBytes
            || game.DetectionExeMtimeMs != onDisk.MtimeUnixMs;
    }
}
