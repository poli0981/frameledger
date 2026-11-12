// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Runtime.InteropServices;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Telemetry;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Sessions;
using FrameLedger.Shared;

namespace FrameLedger.Application.Recording;

/// <summary>
/// One session, end to end (<c>04_CAPTURE</c> §Session recorder): the row's identity and time base, the
/// game and hardware rows, the <c>.partial</c> from before the first record to after the last, the loop,
/// the exit classification, the finalize, the crash policy.
/// </summary>
/// <remarks>
/// <para>
/// <b>The state machine as built.</b> The loop owns <c>Guarded → Injecting → Capturing</c> inside
/// <c>CaptureSession</c> and reports them as one outcome, so the recorder observes: <c>Started</c> (the
/// file exists), <c>Attached</c> (the ring is ours — Tier 1 from here), <c>Ended</c> (with the loop's
/// reason), then <c>Saved</c> / <c>Discarded</c>; a loop that never attached is Tier 2 with the reason.
/// Each transition is a note in the file, which is the breadcrumb <c>19_SAFETY</c> §Crash safety wants
/// written "before injection".
/// </para>
/// <para>
/// <b>Everything here runs on the loop's task.</b> The observer's tick is where the telemetry queue is
/// drained and the <c>.partial</c> flushed; the poller has its own thread and the loop is the only
/// reader of the ring (<c>04_CAPTURE</c> §Threading model).
/// </para>
/// </remarks>
public sealed class SessionRecorder : ISessionRecorder
{
    private readonly ICaptureSessionFactory _sessions;
    private readonly IGameRepository _games;
    private readonly IHardwareSnapshotRepository _snapshots;
    private readonly IHardwareSnapshotSource _hardware;
    private readonly IPartialSessionStore _partials;
    private readonly SessionFinalizer _finalizer;
    private readonly ICrashEventSource _crashes;
    private readonly CrashAutoDisablePolicy _crashPolicy;
    private readonly Func<RecorderOptions, ITelemetryPoller?> _pollers;
    private readonly TimeProvider _clock;
    private readonly RecorderOptions _options;
    private readonly IRecorderPolicy? _policy;
    private readonly ISessionObserver? _observer;
    private readonly IDriverProfileSource? _profiles;
    private readonly UserModeExceptionLapsePolicy? _exceptionLapse;
    private readonly ICrashReporterSightings? _reporters;
    private readonly IAdvancedHardware? _advanced;

    public SessionRecorder(ICaptureSessionFactory sessions, IGameRepository games, IHardwareSnapshotRepository snapshots,
        IHardwareSnapshotSource hardware, IPartialSessionStore partials, SessionFinalizer finalizer, ICrashEventSource crashes,
        Func<RecorderOptions, ITelemetryPoller?> pollers, TimeProvider clock, RecorderOptions? options = null, ISessionObserver? observer = null,
        IRecorderPolicy? policy = null, IDriverProfileSource? profiles = null, UserModeExceptionLapsePolicy? exceptionLapse = null,
        ICrashReporterSightings? reporters = null, IAdvancedHardware? advanced = null)
    {
        // Advanced metrics (owner decision D101): the Agent's one shared reader, held by a session only when the option
        // is on (RecorderOptions.AdvancedMetrics); null in every recorder that has none (the tests, the unshipped host).
        _advanced = advanced;
        // beta.14: the engine crash reporters the watcher saw the game start; null where nothing watches (the tests, the console).
        _reporters = reporters;
        // D33: ends a game's user-mode exception when a session under it ends badly; null in every recorder that has no
        // consent store to write to (the tests, the unshipped host), where no exception can be in force either.
        _exceptionLapse = exceptionLapse;
        // beta.8: the NVIDIA driver profile the game runs under, read beside each session; null in every recorder that has
        // no NVIDIA bridge to ask (the tests, the unshipped host).
        _profiles = profiles;
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
        _partials = partials ?? throw new ArgumentNullException(nameof(partials));
        _finalizer = finalizer ?? throw new ArgumentNullException(nameof(finalizer));
        _crashes = crashes ?? throw new ArgumentNullException(nameof(crashes));
        _pollers = pollers ?? throw new ArgumentNullException(nameof(pollers));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _crashPolicy = new CrashAutoDisablePolicy(games);
        _options = options ?? new RecorderOptions();
        // The second listener (P3 PR-1): the pipe's publisher. Optional, because the unshipped host and every
        // recorder test have nobody to tell; when present it hears each step AFTER the recorder's own work.
        _observer = observer;
        // D16 (P3 PR-3): the Agent re-reads its settings at each session start; the policy is that read, and
        // a recorder without one (the tests, the unshipped host) runs under its baseline options unchanged.
        _policy = policy;
    }

    /// <summary>How long finalising waits for the driver-profile read before storing none: a stuck driver must not hold a session.</summary>
    public static TimeSpan DriverProfileBudget { get; } = TimeSpan.FromSeconds(5);

    public async Task<RecordedSession> RecordAsync(RecordRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        DateTimeOffset startedAt = _clock.GetUtcNow();
        long qpcEpoch = _clock.GetTimestamp();
        Guid guid = request.SessionGuid ?? Guid.NewGuid();
        RecorderOptions options = _policy is null ? _options : await _policy.ResolveAsync(_options, ct).ConfigureAwait(false);

        ExecutableFingerprint fingerprint = request.Observed ?? new ExecutableFingerprint { ExePath = request.NormalisedExePath, SizeBytes = 0, MtimeUnixMs = 0 };
        GameRow game = await _games.EnsureAsync(fingerprint, request.GameName ?? Path.GetFileNameWithoutExtension(request.NormalisedExePath), ct).ConfigureAwait(false);
        long snapshotId = await _snapshots.EnsureAsync(_hardware.Take(), startedAt, ct).ConfigureAwait(false);
        _observer?.Started(new SessionStartedInfo(guid, game.Id, game.Name, request.NormalisedExePath, request.Mode, startedAt, _clock.TimestampFrequency));

        try
        {
            return await RecordStartedAsync(request, game, guid, startedAt, qpcEpoch, snapshotId, options, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Told, then rethrown: the orchestrator logs it as FAULTED and the .partial stays for recovery. The
            // observer hears it so a UI is not left with a session that started and never ended.
            _observer?.Faulted(guid, ex);
            throw;
        }
    }

    private async Task<RecordedSession> RecordStartedAsync(RecordRequest request, GameRow game, Guid guid, DateTimeOffset startedAt,
        long qpcEpoch, long snapshotId, RecorderOptions options, CancellationToken ct)
    {
        ITelemetryPoller? poller = _pollers(options);

        // Held for the whole session - the poller holds it too, and lets go before the finalize reads the last snapshot.
        // With the option off nothing is held, and the shared reader is not started for this session (D101).
        IAdvancedHardware? advanced = options.AdvancedMetrics ? _advanced : null;
        using IDisposable? advancedHold = advanced?.Acquire();
        // The driver profile, read BESIDE the session rather than before it (beta.8): a DRS load takes tens of milliseconds
        // and a launch must not wait on it. The profile the driver applies is fixed when the process starts, so a read that
        // starts here and is awaited at the end describes the run.
        Task<DriverProfileReading>? profile = _profiles is null ? null : Task.Run(() => _profiles.Read(request.NormalisedExePath), CancellationToken.None);
        try
        {
            poller?.Start();
            var header = new PartialHeader
            {
                SessionGuid = guid,
                StartedAt = startedAt,
                QpcEpoch = (ulong)qpcEpoch,
                QpcFrequency = _clock.TimestampFrequency,
                GameId = game.Id,
                SnapshotId = snapshotId,
                ExePath = request.NormalisedExePath,
                Tier = CaptureTier.NotHooked,
                Mode = request.Mode,
                TelemetryDescriptor = poller?.Descriptor,
            };

            using var writer = new PartialSessionWriter(_partials.Create(header), _clock, options.PartialFlushInterval);
            var run = new Run(writer, poller, _clock, _observer, guid, advanced, request.NormalisedExePath);
            writer.Note("started " + request.Mode);
            CaptureOutcome outcome = await RunLoopAsync(request, run, ct).ConfigureAwait(false);
            DateTimeOffset endedAt = _clock.GetUtcNow();
            await run.CompleteAdvancedAsync().ConfigureAwait(false);
            run.FinalFlush();
            writer.Note("ended " + outcome.Reason);

            string? driverProfile = await DriverProfileJsonAsync(profile).ConfigureAwait(false);
            RecordedSession recorded = await FinalizeAsync(request, game, header, outcome, run, endedAt, writer, options, driverProfile, ct).ConfigureAwait(false);
            _observer?.Ended(recorded);
            return recorded;
        }
        finally
        {
            poller?.Dispose();
        }
    }

    private async Task<CaptureOutcome> RunLoopAsync(RecordRequest request, Run run, CancellationToken ct)
    {
        CaptureSession session = _sessions.Create(run);
        return request.Mode == CaptureMode.Launch
            ? await session.RunLaunchedAsync(request.NormalisedExePath, request.Observed, request.PayloadPath, request.Arguments, ct, request.StopToken).ConfigureAwait(false)
            : await session.RunAsync(request.NormalisedExePath, request.Observed, request.PayloadPath, ct, request.StopToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The driver-profile read's column, or null — never a failed session: a read that faults, or takes longer than
    /// <see cref="DriverProfileBudget"/>, stores no profile and the session is finalised as it would have been.
    /// </summary>
    private static async Task<string?> DriverProfileJsonAsync(Task<DriverProfileReading>? read)
    {
        if (read is null)
        {
            return null;
        }

        try
        {
            return DriverProfileRecord.Serialize(await read.WaitAsync(DriverProfileBudget).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or ArgumentException
                                       or ExternalException or DllNotFoundException or EntryPointNotFoundException
                                       or BadImageFormatException or MarshalDirectiveException)
        {
            return null;
        }
    }

    private async Task<RecordedSession> FinalizeAsync(RecordRequest request, GameRow game, PartialHeader header, CaptureOutcome outcome,
        Run run, DateTimeOffset endedAt, PartialSessionWriter writer, RecorderOptions options, string? driverProfile, CancellationToken ct)
    {
        bool hooked = outcome.AttachRefusal == ShmAttachRefusal.Ok;
        // A held Tier-2 session's duration is the game's (2026-09-22), so the Application-log witness applies to it too.
        bool hadPid = hooked || outcome.TargetPid != 0 || outcome.HeldUnhooked;
        // beta.14: the query ties every witness to THIS game's process — its pid, its path — inside the session's window.
        bool targetLeft = outcome.ExitCode is not null || outcome.Reason is SessionEndReason.TargetExited or SessionEndReason.LaunchTargetExited;
        var query = new CrashQuery(request.NormalisedExePath, outcome.TargetPid, header.StartedAt, endedAt + ExitStatusMapper.CrashWitnessGrace, targetLeft);
        CrashReporterSighting? reporter = hadPid && _reporters is { } reporters ? await reporters.FindAsync(query, ct).ConfigureAwait(false) : null;
        bool reporterCounted = reporter is { } seen && CrashReporterRule.Counts(seen, query, endedAt);
        bool crashEvent = hadPid && _crashes.FoundCrash(query);
        ExitStatus exit = ExitStatusMapper.Map(outcome.Reason, outcome.ExitCode, crashEvent, reporterCounted);

        SessionRow skeleton = Skeleton(header, outcome, endedAt, exit, new CrashWitness(crashEvent, reporter?.Image, reporterCounted), hooked) with { DriverProfileJson = driverProfile };
        var input = new FinalizeInput
        {
            Skeleton = skeleton,
            Hooked = hooked ? Hooked(outcome, header.QpcFrequency) : null,
            Sensors = writer.Sensors,
            Advanced = writer.Advanced,
            RetentionKeep = options.RetentionKeep,
            MinimumSessionLength = options.MinimumSessionLength,
            ExePath = request.NormalisedExePath,
        };
        FinalizedSession built = _finalizer.Build(input);

        writer.Note("finalizing " + exit);
        FinalizeOutcome saved = await _finalizer.FinalizeAsync(input, ct).ConfigureAwait(false);
        writer.Dispose();
        _partials.Delete(header.SessionGuid);

        // The row the session was stored under (2026-09-23): the one it started with, or — when that entry was removed
        // and re-imported while the game ran — the one that holds its executable now. A removed game has none, and
        // neither the injection stamp nor the crash policy has a row to write to.
        long ownerId = saved.GameId ?? game.Id;
        CrashPolicyOutcome crash = CrashPolicyOutcome.NotAnEarlyCrash;
        if (hooked && saved.Status != FinalizeStatus.GameRemoved)
        {
            await _games.RecordInjectionAsync(ownerId, run.AttachedAt ?? header.StartedAt, ct).ConfigureAwait(false);
            crash = await _crashPolicy.ApplyAsync(ownerId, exit, outcome.ExitCode, run.AttachedAt, endedAt, ct).ConfigureAwait(false);
        }

        // D33: a session under the game's user-mode exception that ended badly ends the exception — after the row is
        // stored, so the session that ended it is on the page the exception is gone from. D38: during the grant's trial a
        // stored hooked session that did not succeed ends it too, and any end then is for good.
        var trial = new SessionForTrial(ownerId, hooked && saved.Status == FinalizeStatus.Saved, built.Row.FrameCount);
        string? lapsed = _exceptionLapse is null
            ? null
            : await _exceptionLapse.ApplyAsync(request.NormalisedExePath, outcome, exit, trial, ct).ConfigureAwait(false);

        return new RecordedSession
        {
            SessionGuid = header.SessionGuid,
            Outcome = outcome,
            Row = built.Row with { Id = saved.SessionId ?? 0, GameId = ownerId },
            ExitStatus = exit,
            Finalize = saved,
            CrashPolicy = crash,
            CrashEventFound = crashEvent,
            ExceptionLapse = lapsed,
        };
    }

    private static SessionRow Skeleton(PartialHeader h, CaptureOutcome o, DateTimeOffset endedAt, ExitStatus exit, CrashWitness crash, bool hooked)
    {
        string notes = ExitStatusMapper.Describe(o.Reason, o.ExitCode, crash.ApplicationLog, crash.Reporter, crash.ReporterCounted);
        if (o.LaunchError is { } launchError)
        {
            notes += "; launch_error=" + launchError.ToString(CultureInfo.InvariantCulture);
        }

        if (!hooked)
        {
            notes += "; tier2: attach=" + o.AttachRefusal;
            if (o.Verdict.Reason != Domain.AntiCheat.AntiCheatRefusalReason.Allow)
            {
                // Three slots, every one present (beta.8): an empty family used to be dropped, and the signal then read as it.
                notes += "; " + CaptureNotes.GuardSlot(o.Verdict.Reason.ToString(), o.Verdict.Family, o.Verdict.Signal);
            }
        }

        // A hooked session under the game's user-mode exception says so, and names the family (D33): the column is the
        // App's, the notes travel into every bug bundle.
        if (hooked && o.ExceptionFamily is { Length: > 0 } excepted)
        {
            notes += "; " + CaptureNotes.AcExceptionSlot(excepted);
        }

        // D48(b), beta.14: the last re-scan could not read the game's tree as the game exited, and the session ended as
        // that exit rather than as a safety unhook; what the scan said stays with the row.
        if (o.ExitScanReason is { } scanAtExit)
        {
            notes += "; scan_at_exit=" + scanAtExit;
        }

        // The session that turned the game's hooking off says so in its notes (2026-09-22): the row's block names
        // the finding, and the notes travel into every bug bundle.
        if (o.HookingTurnedOff)
        {
            notes += "; hooking-turned-off=" + o.Verdict.Reason + (string.IsNullOrEmpty(o.Verdict.Family) ? "" : "/" + o.Verdict.Family);
        }

        return new SessionRow
        {
            SessionGuid = h.SessionGuid,
            GameId = h.GameId,
            SnapshotId = h.SnapshotId,
            StartedAt = h.StartedAt,
            EndedAt = endedAt,
            QpcEpoch = h.QpcEpoch,
            QpcFrequency = h.QpcFrequency,
            Tier = hooked ? CaptureTier.Hooked : CaptureTier.NotHooked,
            Mode = h.Mode,
            ExitStatus = exit,
            CaptureNotes = notes,
            LateAttach = hooked && h.Mode == CaptureMode.Attach,
            TelemetrySource = h.TelemetryDescriptor,
            OverlayBuildId = hooked ? BuildIdOf(o.Handshake) : null,
            LaunchWaitMs = o.LaunchWait is { } w ? (long)w.TotalMilliseconds : null,
            DrainTicks = hooked ? o.DrainTicks : null,
            ForegroundTicks = hooked ? o.ForegroundTicks : null,
            GuardTicksPublished = hooked ? o.GuardTicksPublished : null,
            AcExceptionFamily = hooked ? o.ExceptionFamily : null,
        }.WithDisplay(o.Display);
    }

    private static AggregationInput Hooked(CaptureOutcome o, long qpcFrequency) => new()
    {
        Records = o.Records,
        GapBefore = o.GapBefore,
        Writer = o.WriterState,
        QpcFrequency = qpcFrequency,
        TotalGaps = o.TotalGaps,
        TotalDropped = o.TotalDropped,
        Modules = o.RuntimeModules,
        Ngx = o.NgxDriver,
    };

    private static string? BuildIdOf(FlShmHandshake handshake)
    {
        string id = handshake.BuildIdString();
        return id.Length == 0 ? null : id;
    }

    /// <summary>
    /// The recorder's ears on the loop: the flush and the telemetry drain, on the loop's task — and, after
    /// each of those, the outside observer's turn with the same tick.
    /// </summary>
    private sealed class Run(PartialSessionWriter writer, ITelemetryPoller? poller, TimeProvider clock, ISessionObserver? observer, Guid guid,
        IAdvancedHardware? advanced, string exePath) : ICaptureObserver
    {
        /// <summary>
        /// How long after the game's process is known its session facts are asked for (owner decision D101): by then
        /// it has a window, so the monitor it presents on can be found. A session shorter than the minimum is discarded
        /// anyway; one that ends before this asks at its end.
        /// </summary>
        private static readonly TimeSpan _sessionFactsAfter = TimeSpan.FromSeconds(15);

        /// <summary>The longest the finalize waits for a session-facts read still in flight.</summary>
        private static readonly TimeSpan _sessionFactsWait = TimeSpan.FromSeconds(5);

        private readonly List<TelemetrySample> _drained = [];
        private CaptureProgress? _last;
        private ulong _adapter;

        private uint? _gamePid;
        private long _gameSince;
        private Task? _sessionFactsRead;
        private volatile IReadOnlyList<AdvancedFact>? _sessionFacts;
        private (IReadOnlyList<AdvancedFact>? Machine, IReadOnlyList<AdvancedSeriesInfo>? Series, AdvancedSources? Sources, IReadOnlyList<AdvancedFact>? Session) _context;

        // What the game told its upscaler (train B): read inside the game by the Overlay, only for a session that
        // records Advanced metrics - the same condition the shared reader is held under.
        private readonly InGameAdvancedSampler? _inGame = advanced is null ? null : new InGameAdvancedSampler();
        private IReadOnlyList<AdvancedReading>? _inGamePending;
        private (IReadOnlyList<AdvancedFact> Facts, AdvancedInGameStatus Status, InGameCallStates Calls) _inGameContext =
            ([], AdvancedInGameStatus.Unknown, default);
        private IReadOnlyList<AdvancedFact> _sessionWithGame = [];

        /// <summary>A session that records Advanced metrics asks the Overlay for what the game tells its upscaler (D111).</summary>
        public bool WantsInGameAdvanced => advanced is not null;

        public DateTimeOffset? AttachedAt { get; private set; }

        public void Attached(int pid, FlShmHandshake handshake)
        {
            AttachedAt = clock.GetUtcNow();
            writer.Note($"attached pid={pid} layout={handshake.LayoutVersion} build={handshake.BuildIdString()}");
            observer?.Attached(guid, pid, handshake);
        }

        /// <summary>
        /// The process the game's memory is read from (beta.12, D43): forwarded to the telemetry thread, and noted in the
        /// <c>.partial</c> when it changes so a recovered session says what it was reading.
        /// </summary>
        public void Measuring(GameProcess target)
        {
            poller?.Follow(target);
            if (advanced is not null && PidOf(target) is uint game && game != _gamePid)
            {
                _gamePid = game;
                _gameSince = clock.GetTimestamp();
            }

            writer.Note(target.IsNone
                ? "memory: none"
                : target.Pid is int pid
                    ? $"memory: pid={pid} held"
                    : "memory: " + string.Join(",", target.Processes.Select(static p => p.Pid.ToString(CultureInfo.InvariantCulture))));
        }

        public void Tick(CaptureProgress progress)
        {
            _last = progress;
            FollowAdapter(progress.AdapterLuid);
            if (_inGame?.Take(progress.InGameAdvanced) is { } fresh)
            {
                // The newest stands until a telemetry sample carries it: both arrive once a second, out of step.
                _inGamePending = fresh;
            }

            DrainTelemetry();
            UpdateAdvancedContext(atEnd: false);
            writer.OnTick(progress);
            observer?.Tick(guid, progress, _drained);
        }

        /// <summary>
        /// Before the last flush (owner decision D101): the session's facts are asked for if they were not yet, a read in
        /// flight is given a few seconds, and the context is set once more — so a session recorded with the option on
        /// always stores a row, if only to say that nothing answered. Does nothing with the option off.
        /// </summary>
        public async Task CompleteAdvancedAsync()
        {
            if (advanced is null)
            {
                return;
            }

            UpdateAdvancedContext(atEnd: true);
            if (_sessionFactsRead is { IsCompleted: false } pending)
            {
                try
                {
                    await pending.WaitAsync(_sessionFactsWait, clock).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    // The driver did not answer in time: the session is stored without its facts rather than held up.
                }
            }

            UpdateAdvancedContext(atEnd: true);
            if (writer.Advanced is null)
            {
                writer.SetAdvancedContext(new AdvancedSessionData([], [], [], AdvancedSources.Unknown with { InGame = InGameStatus() }));
            }
        }

        /// <summary>
        /// Keeps the writer's advanced context current — from the shared reader's latest snapshot, which is a read of
        /// one reference, and from the session's facts once they were read. The facts are read OFF this task: asking
        /// the driver for a profile takes some hundred milliseconds, and this is the capture loop's.
        /// </summary>
        private void UpdateAdvancedContext(bool atEnd)
        {
            if (advanced is null)
            {
                return;
            }

            if (_sessionFactsRead is null && _gamePid is uint pid && (atEnd || clock.GetElapsedTime(_gameSince) >= _sessionFactsAfter))
            {
                var target = new AdvancedSessionTarget(pid, exePath, GdiDeviceName: null);
                _sessionFactsRead = Task.Run(() => _sessionFacts = ReadSessionFacts(advanced, target));
            }

            if (atEnd)
            {
                // The one fact that is a count is brought up to the last publication (InGameAdvancedSampler.Conclude).
                _inGame?.Conclude();
            }

            AdvancedSnapshot? snapshot = advanced.Latest;
            IReadOnlyList<AdvancedFact>? session = _sessionFacts;
            IReadOnlyList<AdvancedFact> game = _inGame?.Facts ?? [];
            AdvancedInGameStatus status = InGameStatus();
            InGameCallStates calls = InGameCalls();
            (IReadOnlyList<AdvancedFact>?, IReadOnlyList<AdvancedSeriesInfo>?, AdvancedSources?, IReadOnlyList<AdvancedFact>?) now = (snapshot?.MachineFacts, snapshot?.Series, snapshot?.Sources, session);
            bool sameGame = ReferenceEquals(game, _inGameContext.Facts) && status == _inGameContext.Status && calls == _inGameContext.Calls;
            if ((snapshot is null && session is null && game.Count == 0)
                || (sameGame && ReferenceEquals(now.Item1, _context.Machine) && ReferenceEquals(now.Item2, _context.Series) && Equals(now.Item3, _context.Sources) && ReferenceEquals(now.Item4, _context.Session)))
            {
                return;
            }

            if (!ReferenceEquals(now.Item4, _context.Session) || !ReferenceEquals(game, _inGameContext.Facts))
            {
                // The driver's facts of the session, then what the game said: one list, rebuilt when either moved.
                _sessionWithGame = game.Count == 0 ? session ?? [] : [.. session ?? [], .. game];
            }

            _context = now;
            _inGameContext = (game, status, calls);
            writer.SetAdvancedContext(new AdvancedSessionData(
                snapshot?.MachineFacts ?? [],
                _sessionWithGame,
                snapshot?.Series ?? [],
                (snapshot?.Sources ?? AdvancedSources.Unknown) with
                {
                    InGame = status,
                    StreamlineConstants = calls.Streamline,
                    FsrFrameGeneration = calls.FrameGeneration,
                    StreamlineFeatureVersion = calls.FeatureVersion,
                    StreamlineFeatureRequirements = calls.FeatureRequirements,
                    StreamlineFeatureSupported = calls.FeatureSupported,
                    StreamlineFeatureLoaded = calls.FeatureLoaded,
                    StreamlineSetFeatureLoaded = calls.SetFeatureLoaded,
                    StreamlineAllocateResources = calls.AllocateResources,
                    StreamlineFreeResources = calls.FreeResources,
                    StreamlineShutdown = calls.Shutdown,
                }));
        }

        /// <summary>
        /// What became of the calls hooked only for Advanced metrics (two since 0.1.0-beta.43, four since
        /// 0.1.0-beta.44, ten since 0.1.0-beta.45), for a session measured inside the game: read, hooked and never called, or not hooked - from
        /// the hook families the capture side last published and from what its publications carried. All null for a
        /// session not measured inside the game: <see cref="AdvancedInGameStatus.NotHooked"/> already says so.
        /// </summary>
        private InGameCallStates InGameCalls()
        {
            if (_inGame is null || AttachedAt is null)
            {
                return default;
            }

            var hooks = (FlHookFamily)(_last?.WriterState.HooksInstalledMask ?? 0u);
            return new InGameCallStates(
                _inGame.Call(FlAdvancedValid.SlConstants, hooks.HasFlag(FlHookFamily.SlConstants)),
                _inGame.Call(FlAdvancedValid.FfxFgConfig, hooks.HasFlag(FlHookFamily.FfxConfigure)),
                _inGame.FeatureCall(FlAdvancedState.FeatureVersionAnswered, hooks.HasFlag(FlHookFamily.SlFeatureVersion)),
                _inGame.FeatureCall(FlAdvancedState.FeatureRequirementsAnswered, hooks.HasFlag(FlHookFamily.SlFeatureRequirements)),
                _inGame.FeatureCall(FlAdvancedState.FeatureSupportAnswered, hooks.HasFlag(FlHookFamily.SlFeatureSupported)),
                _inGame.FeatureCall(FlAdvancedState.FeatureLoadedAnswered, hooks.HasFlag(FlHookFamily.SlFeatureLoaded)),
                _inGame.FeatureCall(InGameAdvancedSampler.FeatureSwitched, hooks.HasFlag(FlHookFamily.SlSetFeatureLoaded)),
                _inGame.FeatureCall(InGameAdvancedSampler.FeatureAllocated, hooks.HasFlag(FlHookFamily.SlAllocateResources)),
                _inGame.FeatureCall(InGameAdvancedSampler.FeatureFreed, hooks.HasFlag(FlHookFamily.SlFreeResources)),
                _inGame.Call(FlAdvancedValid.SlShutdown, hooks.HasFlag(FlHookFamily.SlShutdown)));
        }

        /// <summary>The ten calls' states, compared as one value when deciding whether the session's context moved.</summary>
        [StructLayout(LayoutKind.Auto)]
        private readonly record struct InGameCallStates(
            AdvancedInGameCall? Streamline,
            AdvancedInGameCall? FrameGeneration,
            AdvancedInGameCall? FeatureVersion,
            AdvancedInGameCall? FeatureRequirements,
            AdvancedInGameCall? FeatureSupported,
            AdvancedInGameCall? FeatureLoaded,
            AdvancedInGameCall? SetFeatureLoaded,
            AdvancedInGameCall? AllocateResources,
            AdvancedInGameCall? FreeResources,
            AdvancedInGameCall? Shutdown);

        /// <summary>
        /// What the row says of the entries read inside the game: read, measured inside the game with none of their
        /// calls seen, or not measured inside the game at all.
        /// </summary>
        private AdvancedInGameStatus InGameStatus() => _inGame?.Status(hooked: AttachedAt is not null) ?? AdvancedInGameStatus.Unknown;

        private static IReadOnlyList<AdvancedFact> ReadSessionFacts(IAdvancedHardware advanced, AdvancedSessionTarget target)
        {
            try
            {
                return advanced.ReadSessionFacts(target);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // The reader guards its sources; a throw here is a defect in it, and it costs the session its facts, not itself.
                return [];
            }
        }

        private static uint? PidOf(GameProcess target)
        {
            int? pid = target.IsNone ? null : target.Pid ?? (target.Processes.Count > 0 ? target.Processes[0].Pid : null);
            return pid is > 0 ? (uint)pid.Value : null;
        }

        /// <summary>
        /// The adapter the game presents on (beta.13): the Overlay publishes its LUID at the first present, the telemetry thread
        /// is told once, and the <c>.partial</c> says whether its layers could follow — until then, and where they could not,
        /// they describe the adapter they found first (<c>18_GPU_VENDOR_APIS</c> §L1).
        /// </summary>
        private void FollowAdapter(ulong luid)
        {
            if (luid == 0 || luid == _adapter)
            {
                return;
            }

            _adapter = luid;
            bool followed = poller?.FollowAdapter(luid) ?? false;
            writer.Note(string.Create(CultureInfo.InvariantCulture, $"adapter: luid=0x{luid:x} {(followed ? "followed" : "not followed")}"));
        }

        public void FinalFlush()
        {
            DrainTelemetry();
            if (_last is not null)
            {
                writer.OnTick(_last, force: true);
            }
        }

        private void DrainTelemetry()
        {
            if (poller is null)
            {
                return;
            }

            _drained.Clear();
            poller.Drain(_drained);
            if (_inGamePending is { } pending && _drained.Count > 0)
            {
                // What the game said rides the newest sample of this drain, beside whatever the Agent's own reader
                // gave it: one tick, one set of readings.
                TelemetrySample newest = _drained[^1];
                _drained[^1] = newest with { Advanced = newest.Advanced is { Count: > 0 } own ? [.. own, .. pending] : pending };
                _inGamePending = null;
            }

            writer.AddSensors(_drained);
        }
    }
}
