using FrameLedger.Application.Capture;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Telemetry;
using FrameLedger.Shared;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Ipc;

/// <summary>
/// The recorder's observer that feeds the pipe (P3 PR-1, HANDOFF §P3 decision D13): <c>SessionStarted</c> at
/// the attach, <c>SessionProgress</c> at 1 Hz while a client listens, the finished session's events at the end —
/// and the <see cref="Status"/> snapshot <c>GetStatus</c> answers from.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every callback runs on a session's own task</b>, and sessions run concurrently (one per game), so the only
/// shared state is the session table, under one lock, and the status, swapped whole. Per-session state is
/// touched only from that session's task.
/// </para>
/// <para>
/// <b>Never throws into the loop.</b> The progress computation is guarded and counted
/// (<see cref="ProgressFaults"/>); a publisher that ended a session over a display number would be worse than
/// no display number.
/// </para>
/// </remarks>
public sealed class SessionEventPublisher : ISessionObserver
{
    public static readonly TimeSpan DefaultProgressInterval = TimeSpan.FromSeconds(1);

    private readonly IIpcEventPublisher _pipe;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _interval;
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, Tracked> _sessions = [];
    private AgentStatus _status = AgentStatus.Idle;
    private long _progressPublished;
    private long _progressFaults;

    public SessionEventPublisher(IIpcEventPublisher pipe, TimeProvider clock, TimeSpan? progressInterval = null)
    {
        _pipe = pipe ?? throw new ArgumentNullException(nameof(pipe));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _interval = progressInterval ?? DefaultProgressInterval;
        if (_interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(progressInterval), progressInterval, "the progress interval must be positive");
        }
    }

    /// <summary>The sessions running now; read from any thread.</summary>
    public AgentStatus Status => Volatile.Read(ref _status);

    /// <summary><c>SessionProgress</c> events published so far.</summary>
    public long ProgressPublished => Interlocked.Read(ref _progressPublished);

    /// <summary>Ticks on which the progress computation threw and was swallowed.</summary>
    public long ProgressFaults => Interlocked.Read(ref _progressFaults);

    public void Started(SessionStartedInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        lock (_lock)
        {
            _sessions[info.SessionGuid] = new Tracked(info);
            Republish();
        }
    }

    public void Attached(Guid sessionGuid, int pid, FlShmHandshake handshake)
    {
        Tracked? t;
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionGuid, out t))
            {
                return;
            }

            t.Pid = pid;
            t.AttachedAt = _clock.GetUtcNow();
            t.AttachedTimestamp = _clock.GetTimestamp();
            Republish();
        }

        _pipe.Publish(IpcMessageType.SessionStarted,
            new SessionStartedEvent(sessionGuid, t.Info.GameId, t.Info.GameName, pid, Tier: 1, t.AttachedAt.Value));
    }

    public void Tick(Guid sessionGuid, CaptureProgress progress, IReadOnlyList<TelemetrySample> drained)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(drained);
        Tracked? t;
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionGuid, out t))
            {
                return;
            }
        }

        if (drained.Count > 0)
        {
            t.LastGpu = drained[^1].Sample;
            t.LastSystem = drained[^1].System;
        }

        if (progress.Hold is { } hold && t.AttachedAt is null)
        {
            Held(sessionGuid, t, hold);
            return;
        }

        if (t.AttachedAt is null || !_pipe.HasClients)
        {
            return;
        }

        long now = _clock.GetTimestamp();
        if (t.LastProgress is { } last && _clock.GetElapsedTime(last, now) < _interval)
        {
            return;
        }

        t.LastProgress = now;
        PublishProgress(sessionGuid, t, progress, now);
    }

    public void Ended(RecordedSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Tracked? t;
        lock (_lock)
        {
            _sessions.Remove(session.SessionGuid, out t);
            Republish();
        }

        if (t is not null)
        {
            RecordedSessionEvents.PublishAll(_pipe, session, t.Info, reasonAlreadyPublished: t.ReasonPublished);
        }
    }

    public void Faulted(Guid sessionGuid, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Tracked? t;
        lock (_lock)
        {
            _sessions.Remove(sessionGuid, out t);
            Republish();
        }

        _pipe.Publish(IpcMessageType.CaptureError,
            new CaptureErrorEvent(sessionGuid, CaptureErrorCode.SessionFaulted, $"{exception.GetType().Name}: {exception.Message}"));

        // And the session is over (2026-09-23): without a completion the live card and the tray stayed on a session that no
        // longer ran. Nothing was stored — the .partial stays for recovery — which is what "faulted" says.
        _pipe.Publish(IpcMessageType.SessionCompleted, new SessionCompletedEvent(
            sessionGuid, SessionId: null, Vocabulary.ExitStatusText(Domain.Sessions.ExitStatus.Interrupted), t?.AttachedAt is null ? 2 : 1,
            RecordedSessionEvents.FaultedFinalize, CaptureErrorCode.SessionFaulted, t?.Info.GameId, t?.Info.GameName));
    }

    /// <summary>
    /// A held (Tier-2) session's tick (2026-09-23). The first one announces it — <c>SessionStarted</c> at tier 2 with why,
    /// and the refusal's own event now rather than when the game exits — and every one after feeds <c>SessionHeld</c> at the
    /// progress interval while a client listens. Until this date a held session sent nothing until it ended.
    /// </summary>
    private void Held(Guid sessionGuid, Tracked t, CaptureOutcome hold)
    {
        if (t.Hold is null)
        {
            SessionHold why = RecordedSessionEvents.HoldOf(hold);
            lock (_lock)
            {
                t.Hold = why;
                t.Pid = hold.TargetPid;
                Republish();
            }

            _pipe.Publish(IpcMessageType.SessionStarted,
                new SessionStartedEvent(sessionGuid, t.Info.GameId, t.Info.GameName, hold.TargetPid, Tier: 2, t.Info.StartedAt, why));
            RecordedSessionEvents.PublishReason(_pipe, sessionGuid, hold, t.Info);
            t.ReasonPublished = true;
        }

        if (!_pipe.HasClients)
        {
            return;
        }

        long now = _clock.GetTimestamp();
        if (t.LastProgress is { } last && _clock.GetElapsedTime(last, now) < _interval)
        {
            return;
        }

        t.LastProgress = now;
        _pipe.Publish(IpcMessageType.SessionHeld, new SessionHeldEvent(
            sessionGuid,
            Math.Max(0, (_clock.GetUtcNow() - t.Info.StartedAt).TotalSeconds),
            t.LastGpu?.TempCoreC,
            t.LastSystem.CpuTempC,
            t.LastGpu?.LoadPct,
            t.LastSystem.CpuLoadPct));
    }

    private void PublishProgress(Guid sessionGuid, Tracked t, CaptureProgress progress, long now)
    {
        try
        {
            double elapsed = _clock.GetElapsedTime(t.AttachedTimestamp, now).TotalSeconds;
            SessionProgressEvent e = SessionProgressCalculator.Compute(sessionGuid, progress, t.Info.QpcFrequency, elapsed, t.LastGpu, t.LastSystem);
            _pipe.Publish(IpcMessageType.SessionProgress, e);
            Interlocked.Increment(ref _progressPublished);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Interlocked.Increment(ref _progressFaults);
        }
    }

    /// <summary>Under the lock: the status is the table, projected.</summary>
    private void Republish()
    {
        var sessions = new ActiveSession[_sessions.Count];
        int i = 0;
        foreach (Tracked t in _sessions.Values)
        {
            sessions[i++] = new ActiveSession(
                t.Info.SessionGuid,
                t.Info.GameId,
                t.Info.GameName,
                t.Pid,
                t.AttachedAt is null ? 2 : 1,
                t.AttachedAt ?? t.Info.StartedAt,
                t.AttachedAt is null ? t.Hold : null);
        }

        Volatile.Write(ref _status, new AgentStatus(sessions));
    }

    private sealed class Tracked(SessionStartedInfo info)
    {
        public SessionStartedInfo Info { get; } = info;

        public int Pid { get; set; }

        public DateTimeOffset? AttachedAt { get; set; }

        public long AttachedTimestamp { get; set; }

        public long? LastProgress { get; set; }

        public GpuSample? LastGpu { get; set; }

        public SystemReading LastSystem { get; set; }

        /// <summary>Why a held session measures nothing; set by its first held tick (2026-09-23).</summary>
        public SessionHold? Hold { get; set; }

        /// <summary>The refusal's event went out when the hold began, so the end does not say it again.</summary>
        public bool ReasonPublished { get; set; }
    }
}
