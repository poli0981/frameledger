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

    public SessionRecorder(ICaptureSessionFactory sessions, IGameRepository games, IHardwareSnapshotRepository snapshots,
        IHardwareSnapshotSource hardware, IPartialSessionStore partials, SessionFinalizer finalizer, ICrashEventSource crashes,
        Func<RecorderOptions, ITelemetryPoller?> pollers, TimeProvider clock, RecorderOptions? options = null, ISessionObserver? observer = null,
        IRecorderPolicy? policy = null, IDriverProfileSource? profiles = null)
    {
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
            var run = new Run(writer, poller, _clock, _observer, guid);
            writer.Note("started " + request.Mode);
            CaptureOutcome outcome = await RunLoopAsync(request, run, ct).ConfigureAwait(false);
            DateTimeOffset endedAt = _clock.GetUtcNow();
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
                                       or System.Runtime.InteropServices.ExternalException or DllNotFoundException or EntryPointNotFoundException
                                       or BadImageFormatException or System.Runtime.InteropServices.MarshalDirectiveException)
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
        bool crashEvent = hadPid && _crashes.FoundCrash(Path.GetFileName(request.NormalisedExePath), header.StartedAt, endedAt + ExitStatusMapper.CrashWitnessGrace);
        ExitStatus exit = ExitStatusMapper.Map(outcome.Reason, outcome.ExitCode, crashEvent);

        SessionRow skeleton = Skeleton(header, outcome, endedAt, exit, crashEvent, hooked) with { DriverProfileJson = driverProfile };
        var input = new FinalizeInput
        {
            Skeleton = skeleton,
            Hooked = hooked ? Hooked(outcome, header.QpcFrequency) : null,
            Sensors = writer.Sensors,
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

        return new RecordedSession
        {
            SessionGuid = header.SessionGuid,
            Outcome = outcome,
            Row = built.Row with { Id = saved.SessionId ?? 0, GameId = ownerId },
            ExitStatus = exit,
            Finalize = saved,
            CrashPolicy = crash,
            CrashEventFound = crashEvent,
        };
    }

    private static SessionRow Skeleton(PartialHeader h, CaptureOutcome o, DateTimeOffset endedAt, ExitStatus exit, bool crashEvent, bool hooked)
    {
        string notes = ExitStatusMapper.Describe(o.Reason, o.ExitCode, crashEvent);
        if (o.LaunchError is { } launchError)
        {
            notes += "; launch_error=" + launchError.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
        };
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
    private sealed class Run(PartialSessionWriter writer, ITelemetryPoller? poller, TimeProvider clock, ISessionObserver? observer, Guid guid) : ICaptureObserver
    {
        private readonly List<TelemetrySample> _drained = [];
        private CaptureProgress? _last;

        public DateTimeOffset? AttachedAt { get; private set; }

        public void Attached(int pid, FlShmHandshake handshake)
        {
            AttachedAt = clock.GetUtcNow();
            writer.Note($"attached pid={pid} layout={handshake.LayoutVersion} build={handshake.BuildIdString()}");
            observer?.Attached(guid, pid, handshake);
        }

        public void Tick(CaptureProgress progress)
        {
            _last = progress;
            DrainTelemetry();
            writer.OnTick(progress);
            observer?.Tick(guid, progress, _drained);
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
            writer.AddSensors(_drained);
        }
    }
}
