using FluentAssertions;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>
/// <c>19_SAFETY</c> §Crash &amp; stability safety: two abnormal ends within 60 s of injection, and not one more thing. Since
/// beta.8 an exit code that is not an exception's is no longer a "crash" on the session, and the policy still counts it —
/// a game the hook hung that the user then ended with End task is the shape the policy exists for.
/// </summary>
public sealed class CrashAutoDisablePolicyTests
{
    private static readonly DateTimeOffset _injected = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ACrashSixtyOneSecondsAfterInjectionIsNotAnEarlyCrash()
    {
        CrashAutoDisablePolicy.IsEarlyCrash(ExitStatus.Crashed, null, _injected, _injected.AddSeconds(61)).Should().BeFalse();
        CrashAutoDisablePolicy.IsEarlyCrash(ExitStatus.Crashed, null, _injected, _injected.AddSeconds(60)).Should().BeTrue();
        CrashAutoDisablePolicy.IsEarlyCrash(ExitStatus.Normal, 0, _injected, _injected.AddSeconds(5)).Should().BeFalse();
        CrashAutoDisablePolicy.IsEarlyCrash(ExitStatus.Crashed, null, null, _injected.AddSeconds(5)).Should().BeFalse("nothing was injected");
    }

    [Fact]
    public void ANonZeroExitThatIsNotACrashStillCountsInsideTheWindow()
    {
        CrashAutoDisablePolicy.IsEarlyCrash(ExitStatus.Normal, 1, _injected, _injected.AddSeconds(20)).Should().BeTrue("End task leaves 1: a hang the user ended");
        CrashAutoDisablePolicy.IsEarlyCrash(ExitStatus.Normal, 1, _injected, _injected.AddSeconds(90)).Should().BeFalse("outside the window it is the game's own");
        CrashAutoDisablePolicy.IsEarlyCrash(ExitStatus.Normal, null, _injected, _injected.AddSeconds(20)).Should().BeFalse("still running, or no code");
    }

    [Fact]
    public async Task TheSecondEarlyCrashDisablesHookingWithTheReasonOnTheRow()
    {
        var games = new FakeGameRepository();
        var policy = new CrashAutoDisablePolicy(games);

        CrashPolicyOutcome first = await policy.ApplyAsync(1, ExitStatus.Crashed, null, _injected, _injected.AddSeconds(10), TestContext.Current.CancellationToken);
        CrashPolicyOutcome late = await policy.ApplyAsync(1, ExitStatus.Crashed, null, _injected, _injected.AddSeconds(61), TestContext.Current.CancellationToken);
        CrashPolicyOutcome second = await policy.ApplyAsync(1, ExitStatus.Normal, 1, _injected, _injected.AddSeconds(59), TestContext.Current.CancellationToken);

        first.Should().Be(CrashPolicyOutcome.Counted);
        late.Should().Be(CrashPolicyOutcome.NotAnEarlyCrash, "61 s after injection is the game's own crash");
        second.Should().Be(CrashPolicyOutcome.HookingDisabled, "an ended hang counts as the second");
        games.CrashCount.Should().Be(2, "only the early ones were counted");
        games.Disabled.Should().ContainSingle().Which.Reason.Should().Be(CrashAutoDisablePolicy.Reason);
        CrashAutoDisablePolicy.Reason.Should().Contain("ended abnormally").And.NotContain("crashed twice");
    }
}
