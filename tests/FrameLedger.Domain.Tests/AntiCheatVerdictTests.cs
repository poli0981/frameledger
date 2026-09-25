using FluentAssertions;
using FrameLedger.Domain.AntiCheat;

namespace FrameLedger.Domain.Tests;

public sealed class AntiCheatVerdictTests
{
    [Fact]
    public void DefaultConstructed_IsNotAllowed()
    {
        // If a code path ever forgets to assign a verdict, what it leaves
        // behind must not read as permission. This is the managed half of the
        // static_assert on the native side.
        default(AntiCheatVerdict).IsAllowed.Should().BeFalse();
    }

    /// <summary>
    /// The two allows are the only permitting values: a plain pass, and since D33 (2026-09-26) a pass whose only findings
    /// were the excepted user-mode family's. Every other reason refuses.
    /// </summary>
    [Fact]
    public void OnlyTheTwoAllowsPermit()
    {
        AntiCheatVerdict.Allowed().IsAllowed.Should().BeTrue();
        AntiCheatVerdict.AllowedUnderException("NetEase Yidun", "NEP2.dll").IsAllowed.Should().BeTrue();

        foreach (AntiCheatRefusalReason reason in Enum.GetValues<AntiCheatRefusalReason>())
        {
            if (reason is AntiCheatRefusalReason.Allow or AntiCheatRefusalReason.AllowedUnderUserModeException)
            {
                continue;
            }

            AntiCheatVerdict.Refused(reason, "fam", "sig").IsAllowed.Should().BeFalse();
        }
    }

    [Theory]
    [InlineData(AntiCheatRefusalReason.Allow)]
    [InlineData(AntiCheatRefusalReason.AllowedUnderUserModeException)]
    public void Refused_CannotCarryAnAllow(AntiCheatRefusalReason allow)
    {
        // Making the contradiction unrepresentable rather than merely untested.
        Action act = () => AntiCheatVerdict.Refused(allow, "fam", "sig");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// D33: an allow under the user-mode exception names what it let through, so the session it starts can be marked
    /// with it, and is never a finding that turns the game's hooking off.
    /// </summary>
    [Fact]
    public void AnExceptionAllowNamesWhatItLetThrough()
    {
        AntiCheatVerdict v = AntiCheatVerdict.AllowedUnderException("NetEase Yidun", "NEP2.dll");
        v.RanUnderException.Should().BeTrue();
        v.Family.Should().Be("NetEase Yidun");
        v.Signal.Should().Be("NEP2.dll");
        v.IsFindingAboutTheGame.Should().BeFalse("what an exception let through is not a finding that turns hooking off");

        AntiCheatVerdict native = AntiCheatVerdict.FromNative((int)AntiCheatRefusalReason.AllowedUnderUserModeException, "NetEase Yidun", "NEP2.dll");
        native.IsAllowed.Should().BeTrue();
        native.RanUnderException.Should().BeTrue();
        native.Family.Should().Be("NetEase Yidun", "the native answer keeps the family, unlike a plain allow");

        AntiCheatVerdict.Allowed().RanUnderException.Should().BeFalse();
        default(AntiCheatVerdict).RanUnderException.Should().BeFalse();

        Action nameless = () => AntiCheatVerdict.AllowedUnderException(" ", "NEP2.dll");
        nameless.Should().Throw<ArgumentException>("an exception that names no family let nothing through that anyone can see");
    }

    [Fact]
    public void FromNative_MapsEveryKnownReason()
    {
        foreach (AntiCheatRefusalReason reason in Enum.GetValues<AntiCheatRefusalReason>())
        {
            AntiCheatVerdict v = AntiCheatVerdict.FromNative((int)reason, "fam", "sig");
            v.Reason.Should().Be(reason);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9999)]
    public void FromNative_RefusesAnUnrecognisedCode(int unknown)
    {
        // An unrecognised code means the mirror has drifted from the native
        // enum. A gate that does not understand its own answer must not allow.
        AntiCheatVerdict v = AntiCheatVerdict.FromNative(unknown, "fam", "sig");

        v.IsAllowed.Should().BeFalse();
        v.Signal.Should().Contain("drifted");
    }

    [Fact]
    public void Allowed_CarriesNoSignal()
    {
        AntiCheatVerdict v = AntiCheatVerdict.FromNative(0, "ignored", "ignored");

        v.IsAllowed.Should().BeTrue();
        v.Family.Should().BeEmpty();
        v.Signal.Should().BeEmpty();
    }

    /// <summary>
    /// What turns a game's hooking off (owner decision 2026-09-22, <c>19_SAFETY</c> §What a finding does to the game): an
    /// anti-cheat named IN the game. A machine-wide driver or service, a heuristic, a scan that could not look, the
    /// latch of a past finding and every fact about the payload or the launch are not that.
    /// </summary>
    [Theory]
    [InlineData(AntiCheatRefusalReason.BlockedModule, true)]
    [InlineData(AntiCheatRefusalReason.BlockedExecutable, true)]
    [InlineData(AntiCheatRefusalReason.BlockedStoreId, true)]
    [InlineData(AntiCheatRefusalReason.AntiCheatDirectory, true)]
    [InlineData(AntiCheatRefusalReason.AntiCheatFile, true)]
    [InlineData(AntiCheatRefusalReason.BlockedDriver, false)]
    [InlineData(AntiCheatRefusalReason.BlockedService, false)]
    [InlineData(AntiCheatRefusalReason.SuspiciousUnsigned, false)]
    [InlineData(AntiCheatRefusalReason.ModuleScanFailed, false)]
    [InlineData(AntiCheatRefusalReason.PreScanFailed, false)]
    [InlineData(AntiCheatRefusalReason.RulesIncomplete, false)]
    [InlineData(AntiCheatRefusalReason.PreviouslyBlocked, false)]
    [InlineData(AntiCheatRefusalReason.InjectionFailed, false)]
    [InlineData(AntiCheatRefusalReason.PayloadNotOurs, false)]
    [InlineData(AntiCheatRefusalReason.KillSwitchEngaged, false)]
    [InlineData(AntiCheatRefusalReason.HookNotEnabled, false)]
    public void AFindingAboutTheGameIsWhatTurnsItsHookingOff(AntiCheatRefusalReason reason, bool finding) =>
        AntiCheatVerdict.Refused(reason, "Easy Anti-Cheat", "EasyAntiCheat_EOS.dll").IsFindingAboutTheGame.Should().Be(finding);

    [Fact]
    public void AFindingNeedsAFamilyAndAnEvaluation()
    {
        AntiCheatVerdict.Refused(AntiCheatRefusalReason.BlockedModule, string.Empty, "x.dll").IsFindingAboutTheGame.Should().BeFalse("a finding that names nothing cannot be written as a block");
        default(AntiCheatVerdict).IsFindingAboutTheGame.Should().BeFalse();
        AntiCheatVerdict.Allowed().IsFindingAboutTheGame.Should().BeFalse();
    }
}
