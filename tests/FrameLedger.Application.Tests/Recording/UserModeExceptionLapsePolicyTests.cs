using FluentAssertions;
using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Sessions;
using FrameLedger.Shared;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>
/// D33: which ends of a session under a game's user-mode exception end the exception — a finding about the game, a safety
/// unhook, the Overlay's own stop, a crash while hooked — and that nothing else does; D38: during the grant's trial, a
/// stored hooked session that recorded no frame or did not end normally ends it too.
/// </summary>
public sealed class UserModeExceptionLapsePolicyTests
{
    private static CaptureOutcome Under(SessionEndReason reason, bool hooked = true, bool turnedOff = false, string? family = "NetEase Yidun") => new()
    {
        Reason = reason,
        AttachRefusal = hooked ? ShmAttachRefusal.Ok : ShmAttachRefusal.NotEvaluated,
        HookingTurnedOff = turnedOff,
        ExceptionFamily = family,
    };

    [Theory]
    [InlineData(SessionEndReason.RefusedByGuard, false, true, ExitStatus.Normal, UserModeExceptionLapse.NewFinding)]
    [InlineData(SessionEndReason.SafetyUnhook, true, true, ExitStatus.UnhookedSafety, UserModeExceptionLapse.NewFinding)]
    [InlineData(SessionEndReason.SafetyUnhook, true, false, ExitStatus.UnhookedSafety, UserModeExceptionLapse.SafetyUnhook)]
    [InlineData(SessionEndReason.WriterStoppedBlocklisted, true, false, ExitStatus.Normal, UserModeExceptionLapse.OverlayStopped)]
    [InlineData(SessionEndReason.TargetExited, true, false, ExitStatus.Crashed, UserModeExceptionLapse.SessionCrashed)]
    public void TheseEndsEndIt(SessionEndReason reason, bool hooked, bool turnedOff, ExitStatus exit, string lapse) =>
        UserModeExceptionLapsePolicy.LapseOf(Under(reason, hooked, turnedOff), exit).Should().Be(lapse);

    [Theory]
    [InlineData(SessionEndReason.TargetExited, true, ExitStatus.Normal)]
    [InlineData(SessionEndReason.StoppedByUser, true, ExitStatus.Normal)]
    [InlineData(SessionEndReason.RefusedByGuard, false, ExitStatus.Normal)]
    [InlineData(SessionEndReason.AttachRefused, false, ExitStatus.Crashed)]
    [InlineData(SessionEndReason.KillSwitchEngaged, true, ExitStatus.Normal)]
    public void AnOrdinaryEndOrACrashWithNothingOfOursInsideLeavesIt(SessionEndReason reason, bool hooked, ExitStatus exit) =>
        UserModeExceptionLapsePolicy.LapseOf(Under(reason, hooked), exit).Should().BeNull();

    [Fact]
    public void ASessionUnderNoExceptionIsNeverTheExceptionsBusiness() =>
        UserModeExceptionLapsePolicy.LapseOf(Under(SessionEndReason.SafetyUnhook, turnedOff: true, family: null), ExitStatus.UnhookedSafety).Should().BeNull();

    private static readonly SessionForTrial _stored = new(GameId: 7, StoredHooked: true, FrameCount: 5_000);

    [Theory]
    [InlineData(ExitStatus.Normal, 0L)]
    [InlineData(ExitStatus.Degraded, 5_000L)]
    [InlineData(ExitStatus.Interrupted, 5_000L)]
    public void DuringTheTrialAStoredSessionThatDidNotSucceedEndsIt(ExitStatus exit, long frames) =>
        UserModeExceptionLapsePolicy.EndOf(Under(SessionEndReason.TargetExited), exit, _stored with { FrameCount = frames }, inTrial: true)
            .Should().Be(UserModeExceptionLapse.TrialFailed);

    [Fact]
    public void ASucceededSessionOrOneNotStoredOrOnePastTheTrialIsNotATrialFailure()
    {
        UserModeExceptionLapsePolicy.EndOf(Under(SessionEndReason.TargetExited), ExitStatus.Normal, _stored, inTrial: true).Should().BeNull("it succeeded");
        UserModeExceptionLapsePolicy.EndOf(Under(SessionEndReason.TargetExited), ExitStatus.Normal, _stored with { StoredHooked = false, FrameCount = 0 }, inTrial: true)
            .Should().BeNull("a session too short to keep proves nothing either way");
        UserModeExceptionLapsePolicy.EndOf(Under(SessionEndReason.TargetExited), ExitStatus.Normal, _stored with { FrameCount = 0 }, inTrial: false)
            .Should().BeNull("past the trial only the D33 ends end it");
        UserModeExceptionLapsePolicy.EndOf(Under(SessionEndReason.TargetExited, family: null), ExitStatus.Normal, _stored with { FrameCount = 0 }, inTrial: true)
            .Should().BeNull("a session under no exception is never the exception's business");
        UserModeExceptionLapsePolicy.EndOf(Under(SessionEndReason.TargetExited), ExitStatus.Crashed, _stored, inTrial: true)
            .Should().Be(UserModeExceptionLapse.SessionCrashed, "a more specific reason is said as itself, and still marks the trial failed");
    }

    [Fact]
    public void SucceededIsTheTrialCountsRule()
    {
        UserModeExceptionLapsePolicy.Succeeded(_stored, ExitStatus.Normal).Should().BeTrue();
        UserModeExceptionLapsePolicy.Succeeded(_stored, ExitStatus.Crashed).Should().BeFalse();
        UserModeExceptionLapsePolicy.Succeeded(_stored with { FrameCount = 0 }, ExitStatus.Normal).Should().BeFalse();
        UserModeExceptionLapsePolicy.Succeeded(_stored with { StoredHooked = false }, ExitStatus.Normal).Should().BeFalse();
    }
}
