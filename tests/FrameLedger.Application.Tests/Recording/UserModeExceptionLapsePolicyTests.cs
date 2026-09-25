using FluentAssertions;
using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Sessions;
using FrameLedger.Shared;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>
/// D33: which ends of a session under a game's user-mode exception end the exception — a finding about the game, a safety
/// unhook, the Overlay's own stop, a crash while hooked — and that nothing else does.
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
}
