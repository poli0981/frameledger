using FluentAssertions;
using FrameLedger.Domain.AntiCheat;
using FrameLedger.Infrastructure.AntiCheat;

namespace FrameLedger.Infrastructure.Tests;

/// <summary>
/// Proves the managed <see cref="AntiCheatRefusalReason"/> has not drifted from
/// the native <c>fl::guard::Reason</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the test <c>20_OPEN_QUESTIONS</c> §S15 item 1 exists for. The
/// managed side is a facade, but a facade whose enum has silently diverged
/// shows the user the WRONG refusal — "a driver was found" when a service was —
/// and, worse, could map an unknown value onto <c>Allow</c>.
/// </para>
/// <para>
/// Same discipline as the shm struct mirror: the two sides are compared
/// mechanically rather than by review, because struct and enum drift between
/// the layers is the most dangerous silent bug in this architecture.
/// </para>
/// </remarks>
public sealed class GuardMirrorTests
{
    [Fact]
    public void TheManagedEnumHasExactlyAsManyValuesAsTheNativeOne()
    {
        int managed = Enum.GetValues<AntiCheatRefusalReason>().Length;

        NativeAntiCheatGuard.NativeReasonCount().Should().Be(managed,
            "a value added on one side and forgotten on the other maps a refusal onto the wrong reason");
    }

    [Fact]
    public void EveryManagedReasonNamesTheSameThingNatively()
    {
        foreach (AntiCheatRefusalReason reason in Enum.GetValues<AntiCheatRefusalReason>())
        {
            string native = NativeAntiCheatGuard.NativeReasonName((int)reason);

            native.Should().Be(reason.ToString(),
                $"managed {reason} = {(int)reason} must be the same code the native guard calls {native}");
        }
    }

    /// <summary>
    /// The user's bypass (owner decision 2026-09-21) overrules "the guard's judgement", and that phrase is a LIST on each
    /// side of the ABI. Held against each other for every reason, so neither can grow alone. The one deliberate
    /// difference is managed-only: <see cref="AntiCheatRefusalReason.PreviouslyBlocked"/> is the latch a past judgement
    /// left on the row, which the native guard never produces and therefore never classifies.
    /// </summary>
    [Fact]
    public void WhatCountsAsTheGuardsJudgementIsTheSameListOnBothSides()
    {
        foreach (AntiCheatRefusalReason reason in Enum.GetValues<AntiCheatRefusalReason>())
        {
            bool native = NativeAntiCheatGuard.NativeIsGuardJudgement((int)reason);
            bool managed = AntiCheatVerdict.IsGuardJudgement(reason);
            if (reason == AntiCheatRefusalReason.PreviouslyBlocked)
            {
                native.Should().BeFalse("the native guard never produces the managed latch");
                managed.Should().BeTrue("the latch IS a past judgement, and the bypass overrules it");
                continue;
            }

            managed.Should().Be(native, $"{reason} must be a judgement on both sides or on neither");
        }

        AntiCheatVerdict.IsGuardJudgement(AntiCheatRefusalReason.Allow).Should().BeFalse();
        AntiCheatVerdict.IsGuardJudgement(AntiCheatRefusalReason.AllowedUnderUserBypass).Should().BeFalse();
        AntiCheatVerdict.IsGuardJudgement(AntiCheatRefusalReason.PayloadNotOurs).Should().BeFalse("a foreign payload is a fact, and no acknowledgement loads one");
        AntiCheatVerdict.IsGuardJudgement(AntiCheatRefusalReason.KillSwitchEngaged).Should().BeFalse();
        NativeAntiCheatGuard.NativeIsGuardJudgement(-1).Should().BeFalse();
        NativeAntiCheatGuard.NativeIsGuardJudgement(9999).Should().BeFalse();
    }

    [Fact]
    public void AnOutOfRangeCodeReturnsNothing_RatherThanAPlausibleName()
    {
        // Returning "Unknown" would let drift look like a legitimate value.
        NativeAntiCheatGuard.NativeReasonName(-1).Should().BeEmpty();
        NativeAntiCheatGuard.NativeReasonName(9999).Should().BeEmpty();
    }

    [Fact]
    public void AllowIsZeroOnBothSides()
    {
        // The value the whole gate turns on.
        ((int)AntiCheatRefusalReason.Allow).Should().Be(0);
        NativeAntiCheatGuard.NativeReasonName(0).Should().Be("Allow");
    }
}
