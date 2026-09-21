using FluentAssertions;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Ipc;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Tests.Recording;
using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Sessions;
using FrameLedger.Shared;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Tests.Ipc;

/// <summary>
/// The reason → event table (<c>07_IPC</c> §Messages, Agent → UI), and what each event carries. The safety
/// events are the ones <c>08_UI</c> forbids collapsing into a toast, so which reasons produce one is pinned here.
/// </summary>
public sealed class RecordedSessionEventsTests
{
    private static readonly SessionStartedInfo _info = new(Guid.NewGuid(), 7, "Title", @"C:\Games\Title\game.exe", CaptureMode.Attach, SessionFixtures.StartedAt, SessionFixtures.QpcFrequency);

    [Theory]
    [InlineData(SessionEndReason.RefusedByGuard, RecordedSessionEvents.Kind.Refused)]
    [InlineData(SessionEndReason.RefusedPreviouslyBlocked, RecordedSessionEvents.Kind.Refused)]
    [InlineData(SessionEndReason.PreScanCouldNotVerify, RecordedSessionEvents.Kind.Refused)]
    [InlineData(SessionEndReason.RefusedKillSwitch, RecordedSessionEvents.Kind.Refused)]
    [InlineData(SessionEndReason.SafetyUnhook, RecordedSessionEvents.Kind.SafetyUnhook)]
    [InlineData(SessionEndReason.SupervisionLost, RecordedSessionEvents.Kind.Degraded)]
    [InlineData(SessionEndReason.WriterSelfDisabled, RecordedSessionEvents.Kind.Degraded)]
    [InlineData(SessionEndReason.WriterStoppedBlocklisted, RecordedSessionEvents.Kind.Degraded)]
    [InlineData(SessionEndReason.WriterNeverInstalledHooks, RecordedSessionEvents.Kind.Degraded)]
    [InlineData(SessionEndReason.SupervisionFaulted, RecordedSessionEvents.Kind.Degraded)]
    [InlineData(SessionEndReason.KillSwitchEngaged, RecordedSessionEvents.Kind.Degraded)]
    [InlineData(SessionEndReason.AttachRefused, RecordedSessionEvents.Kind.AttachError)]
    [InlineData(SessionEndReason.LaunchCannotStart, RecordedSessionEvents.Kind.TargetError)]
    [InlineData(SessionEndReason.TargetAmbiguous, RecordedSessionEvents.Kind.TargetError)]
    [InlineData(SessionEndReason.TargetUnreadable, RecordedSessionEvents.Kind.Refused)]
    [InlineData(SessionEndReason.TargetNotRunning, RecordedSessionEvents.Kind.TargetError)]
    [InlineData(SessionEndReason.TargetExited, RecordedSessionEvents.Kind.None)]
    [InlineData(SessionEndReason.RefusedHookNotEnabled, RecordedSessionEvents.Kind.None)]
    [InlineData(SessionEndReason.RefusedConsentMissing, RecordedSessionEvents.Kind.None)]
    [InlineData(SessionEndReason.Running, RecordedSessionEvents.Kind.None)]
    [InlineData(SessionEndReason.StoppedByUser, RecordedSessionEvents.Kind.None)]
    public void EveryReasonHasOneKind(SessionEndReason reason, RecordedSessionEvents.Kind expected) =>
        RecordedSessionEvents.Classify(reason).Should().Be(expected);

    [Fact]
    public void EveryReasonIsClassifiedSomewhere()
    {
        foreach (SessionEndReason reason in Enum.GetValues<SessionEndReason>())
        {
            Action classify = () => RecordedSessionEvents.Classify(reason);
            classify.Should().NotThrow();
        }
    }

    [Fact]
    public void ANormalEndPublishesSessionCompletedAlone()
    {
        var pipe = new RecordingPublisher();
        RecordedSession session = Session(SessionEndReason.TargetExited, ExitStatus.Normal, new FinalizeOutcome(FinalizeStatus.Saved, 42, 0));

        RecordedSessionEvents.PublishAll(pipe, session, _info);

        pipe.Published.Should().ContainSingle();
        SessionCompletedEvent done = pipe.Of<SessionCompletedEvent>().Single();
        done.SessionGuid.Should().Be(session.SessionGuid);
        done.SessionId.Should().Be(42);
        done.ExitStatus.Should().Be("normal", "the row's vocabulary, so the UI has one spelling");
        done.Tier.Should().Be(1);
        done.Finalize.Should().Be("saved");
        done.Reason.Should().Be("TargetExited");
    }

    [Fact]
    public void AGuardRefusalNamesTheFamilyAndSignalAndThenCompletes()
    {
        var pipe = new RecordingPublisher();
        RecordedSession session = Session(SessionEndReason.RefusedByGuard, ExitStatus.Normal, new FinalizeOutcome(FinalizeStatus.Discarded, null, 0),
            verdict: AntiCheatVerdict.Refused(AntiCheatRefusalReason.BlockedModule, "eac", "EasyAntiCheat.dll"), tier: CaptureTier.NotHooked);

        RecordedSessionEvents.PublishAll(pipe, session, _info);

        pipe.Published.Select(static p => p.Type).Should().Equal(IpcMessageType.CaptureRefused, IpcMessageType.SessionCompleted);
        CaptureRefusedEvent refused = pipe.Of<CaptureRefusedEvent>().Single();
        refused.GameId.Should().Be(7);
        refused.GameName.Should().Be("Title");
        refused.Reason.Should().Be("RefusedByGuard");
        refused.Family.Should().Be("eac");
        refused.Signal.Should().Be("EasyAntiCheat.dll");
        SessionCompletedEvent done = pipe.Of<SessionCompletedEvent>().Single();
        done.SessionId.Should().BeNull();
        done.Finalize.Should().Be("discarded");
        done.Tier.Should().Be(2);
    }

    [Fact]
    public void ASafetyUnhookCarriesTheVerdictThatFired()
    {
        var pipe = new RecordingPublisher();
        RecordedSession session = Session(SessionEndReason.SafetyUnhook, ExitStatus.UnhookedSafety, new FinalizeOutcome(FinalizeStatus.Saved, 9, 0),
            verdict: AntiCheatVerdict.Refused(AntiCheatRefusalReason.BlockedModule, "battleye", "BEClient_x64.dll"));

        RecordedSessionEvents.PublishAll(pipe, session, _info);

        SafetyUnhookEvent unhook = pipe.Of<SafetyUnhookEvent>().Single();
        unhook.SessionGuid.Should().Be(session.SessionGuid);
        unhook.Family.Should().Be("battleye");
        unhook.Signal.Should().Be("BEClient_x64.dll");
        pipe.Of<SessionCompletedEvent>().Single().ExitStatus.Should().Be("unhooked_safety");
    }

    [Fact]
    public void AMidSessionStopThatIsNotTheGuardsIsDegradedFromTierOneToTwo()
    {
        var pipe = new RecordingPublisher();
        RecordedSessionEvents.PublishAll(pipe, Session(SessionEndReason.WriterSelfDisabled, ExitStatus.Degraded, new FinalizeOutcome(FinalizeStatus.Saved, 1, 0)), _info);

        CaptureDegradedEvent degraded = pipe.Of<CaptureDegradedEvent>().Single();
        degraded.From.Should().Be(1);
        degraded.To.Should().Be(2);
        degraded.Reason.Should().Be("WriterSelfDisabled");
    }

    [Theory]
    [InlineData(ShmAttachRefusal.BuildIdMismatch, CaptureErrorCode.RingVersionMismatch)]
    [InlineData(ShmAttachRefusal.LayoutVersionMismatch, CaptureErrorCode.RingVersionMismatch)]
    [InlineData(ShmAttachRefusal.RecordSizeMismatch, CaptureErrorCode.RingVersionMismatch)]
    [InlineData(ShmAttachRefusal.CapacityInvalid, CaptureErrorCode.AttachRefused)]
    [InlineData(ShmAttachRefusal.Incomplete, CaptureErrorCode.AttachRefused)]
    public void ARefusedAttachIsACaptureErrorWithTheRingCode(ShmAttachRefusal refusal, string code)
    {
        var pipe = new RecordingPublisher();
        RecordedSession session = Session(SessionEndReason.AttachRefused, ExitStatus.Normal, new FinalizeOutcome(FinalizeStatus.Discarded, null, 0), tier: CaptureTier.NotHooked) with
        {
            Outcome = new CaptureOutcome { Reason = SessionEndReason.AttachRefused, AttachRefusal = refusal },
        };

        RecordedSessionEvents.PublishAll(pipe, session, _info);

        CaptureErrorEvent error = pipe.Of<CaptureErrorEvent>().Single();
        error.Code.Should().Be(code);
        error.SessionGuid.Should().Be(session.SessionGuid);
        error.Message.Should().Contain(refusal.ToString());
    }

    private static RecordedSession Session(SessionEndReason reason, ExitStatus exit, FinalizeOutcome finalize, AntiCheatVerdict? verdict = null, CaptureTier tier = CaptureTier.Hooked)
    {
        var guid = Guid.NewGuid();
        return new RecordedSession
        {
            SessionGuid = guid,
            Outcome = new CaptureOutcome { Reason = reason, Verdict = verdict ?? AntiCheatVerdict.Allowed(), AttachRefusal = tier == CaptureTier.Hooked ? ShmAttachRefusal.Ok : ShmAttachRefusal.NotEvaluated },
            Row = SessionFixtures.Skeleton(guid, tier: tier),
            ExitStatus = exit,
            Finalize = finalize,
            CrashPolicy = CrashPolicyOutcome.NotAnEarlyCrash,
            CrashEventFound = false,
        };
    }
}
