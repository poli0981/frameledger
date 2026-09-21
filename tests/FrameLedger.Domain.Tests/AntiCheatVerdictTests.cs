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

    [Fact]
    public void Allowed_IsTheOnlyPermittingValue()
    {
        AntiCheatVerdict.Allowed().IsAllowed.Should().BeTrue();

        foreach (AntiCheatRefusalReason reason in Enum.GetValues<AntiCheatRefusalReason>())
        {
            if (reason == AntiCheatRefusalReason.Allow)
            {
                continue;
            }

            AntiCheatVerdict.Refused(reason, "fam", "sig").IsAllowed.Should().BeFalse();
        }
    }

    [Fact]
    public void Refused_CannotCarryAllow()
    {
        // Making the contradiction unrepresentable rather than merely untested.
        Action act = () => AntiCheatVerdict.Refused(AntiCheatRefusalReason.Allow, "fam", "sig");
        act.Should().Throw<ArgumentOutOfRangeException>();
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
    /// The user's bypass (owner decision 2026-09-21) has a verdict of its own, and the point of it is what it is NOT:
    /// not an allow, so every caller written before it keeps refusing; not a default, so nothing reads as one by
    /// accident; and still carrying what the guard found.
    /// </summary>
    [Fact]
    public void AllowedUnderUserBypassIsNeitherAnAllowNorTheDefaultAndKeepsWhatWasFound()
    {
        AntiCheatVerdict under = AntiCheatVerdict.FromNative((int)AntiCheatRefusalReason.AllowedUnderUserBypass, "Easy Anti-Cheat", "EasyAntiCheat_EOS.dll");

        under.IsAllowed.Should().BeFalse();
        under.IsAllowedUnderBypass.Should().BeTrue();
        under.Family.Should().Be("Easy Anti-Cheat");
        under.Signal.Should().Be("EasyAntiCheat_EOS.dll");

        default(AntiCheatVerdict).IsAllowedUnderBypass.Should().BeFalse("a verdict nobody produced overruled nothing");
        AntiCheatVerdict.Allowed().IsAllowedUnderBypass.Should().BeFalse();
        AntiCheatVerdict.Refused(AntiCheatRefusalReason.BlockedModule, "x", "y").IsAllowedUnderBypass.Should().BeFalse();
        ((int)AntiCheatRefusalReason.AllowedUnderUserBypass).Should().Be(28, "the values cross a C ABI and are append-only");
    }

    [Theory]
    [InlineData(AntiCheatRefusalReason.BlockedModule, true)]
    [InlineData(AntiCheatRefusalReason.BlockedDriver, true)]
    [InlineData(AntiCheatRefusalReason.SuspiciousUnsigned, true)]
    [InlineData(AntiCheatRefusalReason.ModuleScanFailed, true)]
    [InlineData(AntiCheatRefusalReason.RulesIncomplete, true)]
    [InlineData(AntiCheatRefusalReason.PreScanFailed, true)]
    [InlineData(AntiCheatRefusalReason.PreviouslyBlocked, true)]
    [InlineData(AntiCheatRefusalReason.Allow, false)]
    [InlineData(AntiCheatRefusalReason.InjectionFailed, false)]
    [InlineData(AntiCheatRefusalReason.TargetIsWow64, false)]
    [InlineData(AntiCheatRefusalReason.PayloadNotOurs, false)]
    [InlineData(AntiCheatRefusalReason.HookNotEnabled, false)]
    [InlineData(AntiCheatRefusalReason.ConsentMissing, false)]
    [InlineData(AntiCheatRefusalReason.KillSwitchEngaged, false)]
    [InlineData(AntiCheatRefusalReason.LaunchTargetExited, false)]
    [InlineData(AntiCheatRefusalReason.TargetIsVulkanLayered, false)]
    [InlineData(AntiCheatRefusalReason.AllowedUnderUserBypass, false)]
    public void ABypassOverrulesAJudgementAndNeverAFact(AntiCheatRefusalReason reason, bool judgement) =>
        AntiCheatVerdict.IsGuardJudgement(reason).Should().Be(judgement);
}
