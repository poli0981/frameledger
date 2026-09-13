using FluentAssertions;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Ipc;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Telemetry;
using FrameLedger.Application.Tests.Recording;
using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Sessions;
using FrameLedger.Shared;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Tests.Ipc;

/// <summary>
/// The recorder's observer as the pipe's feed: status follows the session table, <c>SessionStarted</c> waits for
/// the attach, progress goes out at the interval and only to a listening client, and the end publishes the
/// finished session's events. The clock is manual so "one second" is a number, not a wait.
/// </summary>
public sealed class SessionEventPublisherTests
{
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = SessionFixtures.StartedAt;

        public override DateTimeOffset GetUtcNow() => Now;

        public override long GetTimestamp() => (Now - DateTimeOffset.UnixEpoch).Ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public void Advance(TimeSpan by) => Now += by;
    }

    private static readonly SessionStartedInfo _info = new(Guid.NewGuid(), 7, "Title", @"C:\Games\Title\game.exe", CaptureMode.Attach, SessionFixtures.StartedAt, SessionFixtures.QpcFrequency);

    private static CaptureProgress Progress(int records) => new()
    {
        Records = SessionFixtures.Stream(records, FlMeasured.OutputRes),
        GapBefore = [],
        WriterState = new FlWriterState { Status = (uint)FlStatus.Ready, HooksInstalledMask = 0x1, RuntimeCensus = (uint)FlRuntimeCensus.Ran },
        TotalDropped = 0,
        TotalGaps = 0,
        DrainTicks = 1,
        ForegroundTicks = 1,
        GuardTicksPublished = 1,
        TouchQpc = [],
        RuntimeModules = RuntimeModuleSet.Empty,
        NgxDriver = NgxDriverState.NotRun,
    };

    [Fact]
    public void StatusFollowsTheSessionFromRecordingToCapturingToIdle()
    {
        var pipe = new RecordingPublisher();
        var clock = new ManualClock();
        var publisher = new SessionEventPublisher(pipe, clock);
        publisher.Status.State.Should().Be(AgentStatus.IdleState);

        publisher.Started(_info);
        publisher.Status.State.Should().Be(AgentStatus.RecordingState, "a session runs and nothing is hooked yet");
        publisher.Status.Sessions.Single().Tier.Should().Be(2);
        pipe.Published.Should().BeEmpty("nothing is published before the attach");

        publisher.Attached(_info.SessionGuid, 4242, default);
        publisher.Status.State.Should().Be(AgentStatus.CapturingState);
        publisher.Status.Sessions.Single().Pid.Should().Be(4242);
        SessionStartedEvent started = pipe.Of<SessionStartedEvent>().Single();
        started.SessionGuid.Should().Be(_info.SessionGuid);
        started.GameId.Should().Be(7);
        started.Pid.Should().Be(4242);
        started.Tier.Should().Be(1);
        started.StartedAt.Should().Be(clock.Now);

        publisher.Ended(Recorded(_info.SessionGuid));
        publisher.Status.State.Should().Be(AgentStatus.IdleState);
        pipe.Published.Select(static p => p.Type).Should().Equal(IpcMessageType.SessionStarted, IpcMessageType.SessionCompleted);
    }

    [Fact]
    public void ProgressGoesOutOncePerIntervalAndOnlyWhileAClientListens()
    {
        var pipe = new RecordingPublisher();
        var clock = new ManualClock();
        var publisher = new SessionEventPublisher(pipe, clock);
        publisher.Started(_info);
        publisher.Attached(_info.SessionGuid, 4242, default);

        publisher.Tick(_info.SessionGuid, Progress(100), []);
        publisher.ProgressPublished.Should().Be(1, "the first tick after the attach publishes at once");

        clock.Advance(TimeSpan.FromMilliseconds(100));
        publisher.Tick(_info.SessionGuid, Progress(110), []);
        clock.Advance(TimeSpan.FromMilliseconds(800));
        publisher.Tick(_info.SessionGuid, Progress(190), []);
        publisher.ProgressPublished.Should().Be(1, "100 ms drain ticks are rate-limited to the interval");

        clock.Advance(TimeSpan.FromMilliseconds(100));
        publisher.Tick(_info.SessionGuid, Progress(200), []);
        publisher.ProgressPublished.Should().Be(2);

        pipe.HasClients = false;
        clock.Advance(TimeSpan.FromSeconds(5));
        publisher.Tick(_info.SessionGuid, Progress(700), []);
        publisher.ProgressPublished.Should().Be(2, "no client, no progress computation");

        SessionProgressEvent last = pipe.Of<SessionProgressEvent>().Last();
        last.ElapsedS.Should().BeApproximately(1.0, 0.001);
        last.PresentedFps5s.Should().BeApproximately(100, 1.5);
        publisher.ProgressFaults.Should().Be(0);
    }

    [Fact]
    public void TheNewestTelemetrySampleRidesOnTheNextProgress()
    {
        var pipe = new RecordingPublisher();
        var clock = new ManualClock();
        var publisher = new SessionEventPublisher(pipe, clock);
        publisher.Started(_info);
        publisher.Attached(_info.SessionGuid, 1, default);

        List<TelemetrySample> drained = SessionFixtures.Sensors(seconds: 3, temp: 60);
        publisher.Tick(_info.SessionGuid, Progress(100), drained);

        pipe.Of<SessionProgressEvent>().Single().GpuTempC.Should().Be(62, "the last drained sample (60 + 2) is the newest");
    }

    [Fact]
    public void ATickForAnUnknownSessionAndBeforeTheAttachPublishesNothing()
    {
        var pipe = new RecordingPublisher();
        var publisher = new SessionEventPublisher(pipe, new ManualClock());

        publisher.Tick(Guid.NewGuid(), Progress(10), []);
        publisher.Started(_info);
        publisher.Tick(_info.SessionGuid, Progress(10), []);

        pipe.Published.Should().BeEmpty();
        publisher.ProgressPublished.Should().Be(0);
    }

    [Fact]
    public void AFaultedSessionEndsWithCaptureErrorAndLeavesTheStatus()
    {
        var pipe = new RecordingPublisher();
        var publisher = new SessionEventPublisher(pipe, new ManualClock());
        publisher.Started(_info);

        publisher.Faulted(_info.SessionGuid, new InvalidOperationException("the ledger is locked"));

        publisher.Status.State.Should().Be(AgentStatus.IdleState);
        CaptureErrorEvent error = pipe.Of<CaptureErrorEvent>().Single();
        error.SessionGuid.Should().Be(_info.SessionGuid);
        error.Code.Should().Be(CaptureErrorCode.SessionFaulted);
        error.Message.Should().Contain("InvalidOperationException").And.Contain("the ledger is locked");
        pipe.Of<SessionCompletedEvent>().Should().BeEmpty("nothing was finalized");
    }

    [Fact]
    public void TwoSessionsAreTrackedApartAndTheHookedOneIsTheStatusPrimary()
    {
        var pipe = new RecordingPublisher();
        var publisher = new SessionEventPublisher(pipe, new ManualClock());
        SessionStartedInfo other = _info with { SessionGuid = Guid.NewGuid(), GameId = 8, GameName = "Other" };

        publisher.Started(_info);
        publisher.Started(other);
        publisher.Attached(other.SessionGuid, 99, default);

        StatusAck ack = publisher.Status.ToAck();
        ack.State.Should().Be(AgentStatus.CapturingState);
        ack.ActiveSession!.GameId.Should().Be(8);
        ack.ActiveSessions.Should().HaveCount(2);
    }

    private static RecordedSession Recorded(Guid guid) => new()
    {
        SessionGuid = guid,
        Outcome = new CaptureOutcome { Reason = SessionEndReason.TargetExited, Verdict = AntiCheatVerdict.Allowed(), AttachRefusal = ShmAttachRefusal.Ok },
        Row = SessionFixtures.Skeleton(guid),
        ExitStatus = ExitStatus.Normal,
        Finalize = new FinalizeOutcome(FinalizeStatus.Saved, 5, 0),
        CrashPolicy = CrashPolicyOutcome.NotAnEarlyCrash,
        CrashEventFound = false,
    };
}
