// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Input;

namespace FrameLedger.Domain.Tests.Input;

/// <summary>
/// Who a controller is by its two USB ids, what XInput's words mean, and a battery's readings added up over a session
/// (<c>03_METRICS</c> §Input).
/// </summary>
public sealed class ControllerAndBatteryTests
{
    private static BatteryReading Percent(int level, BatteryState state = BatteryState.Discharging) => new(state, BatteryScale.Percent, level);

    [Theory]
    [InlineData(ControllerCatalog.Microsoft, false, ControllerFamily.Xbox)]
    [InlineData(ControllerCatalog.Sony, false, ControllerFamily.PlayStation)]
    [InlineData(ControllerCatalog.Nintendo, false, ControllerFamily.Nintendo)]
    [InlineData(0x2DC8, true, ControllerFamily.Xbox)] // another maker's pad that Windows presents through XInput
    [InlineData(0x2DC8, false, ControllerFamily.Other)]
    [InlineData(0, true, ControllerFamily.Xbox)] // one of XInput's pads that nothing named
    public void AFamilyIsTheMakerOrWhatWindowsPresentsThePadAs(int vendor, bool xinput, ControllerFamily family) =>
        ControllerCatalog.FamilyOf(vendor, xinput).Should().Be(family);

    [Fact]
    public void AModelHasANameOnlyWhereThisBuildListsIt()
    {
        ControllerCatalog.ModelOf(ControllerCatalog.Sony, ControllerCatalog.DualSense).Should().Be("DualSense");
        ControllerCatalog.ModelOf(ControllerCatalog.Sony, ControllerCatalog.DualSenseEdge).Should().Be("DualSense Edge");
        ControllerCatalog.ModelOf(ControllerCatalog.Microsoft, 0x0B12).Should().Be("Xbox Wireless Controller");
        ControllerCatalog.ModelOf(ControllerCatalog.Microsoft, 0xFFFF).Should().BeNull("a wrong name would be worse than none");
        ControllerCatalog.ModelOf(0, 0).Should().BeNull();
    }

    [Fact]
    public void SteamsVirtualGamepadIsAPadAProgramPresents()
    {
        ControllerCatalog.IsVirtual(ControllerCatalog.Valve, ControllerCatalog.SteamVirtualGamepad).Should().BeTrue();
        ControllerCatalog.IsVirtual(ControllerCatalog.Valve, 0x1142).Should().BeFalse("a Steam Controller is one a player holds");
        ControllerCatalog.IsVirtual(ControllerCatalog.Microsoft, 0x028E).Should().BeFalse();
    }

    [Fact]
    public void OnlySonysTwoDualSensesAreReadByTheDualSenseParser()
    {
        ControllerCatalog.IsDualSense(ControllerCatalog.Sony, ControllerCatalog.DualSense).Should().BeTrue();
        ControllerCatalog.IsDualSense(ControllerCatalog.Sony, ControllerCatalog.DualSenseEdge).Should().BeTrue();
        ControllerCatalog.IsDualSense(ControllerCatalog.Sony, 0x09CC).Should().BeFalse("a DualShock 4 has another layout");
        ControllerCatalog.IsDualSense(ControllerCatalog.Microsoft, ControllerCatalog.DualSense).Should().BeFalse();
    }

    [Fact]
    public void AnXInputPadAtRestIsNotInUse() =>
        XInputPad.IsActive(0, 0, 0, 0, 0, 0, 0).Should().BeFalse();

    [Theory]
    [InlineData(0x1000, 0, 0, 0, 0, 0, 0)] // a button
    [InlineData(0, 31, 0, 0, 0, 0, 0)] // the left trigger past the threshold
    [InlineData(0, 0, 255, 0, 0, 0, 0)] // the right trigger
    [InlineData(0, 0, 0, 7850, 0, 0, 0)] // the left stick just outside its dead zone
    [InlineData(0, 0, 0, 0, -32768, 0, 0)] // held fully down
    [InlineData(0, 0, 0, 0, 0, 6200, 6200)] // the right stick, diagonally outside
    public void AButtonATriggerOrAStickOutsideItsDeadZoneIsAPadInUse(int buttons, int lt, int rt, int lx, int ly, int rx, int ry) =>
        XInputPad.IsActive(buttons, lt, rt, lx, ly, rx, ry).Should().BeTrue();

    [Theory]
    [InlineData(0, 30, 30, 0, 0, 0, 0)] // triggers at the threshold
    [InlineData(0, 0, 0, 7849, 0, 0, 0)] // on the edge of the left dead zone
    [InlineData(0, 0, 0, 5000, 5000, 0, 0)] // drift
    [InlineData(0, 0, 0, 0, 0, 8689, 0)] // on the edge of the right dead zone
    public void DriftInsideADeadZoneIsNotUse(int buttons, int lt, int rt, int lx, int ly, int rx, int ry) =>
        XInputPad.IsActive(buttons, lt, rt, lx, ly, rx, ry).Should().BeFalse();

    [Fact]
    public void XInputsBatteryWordsAreAWiredPadFourLevelsOrNothing()
    {
        XInputPad.Battery(type: 1, level: 0).Should().Be(BatteryReading.Wired);
        XInputPad.Battery(type: 2, level: 0).Should().Be(new BatteryReading(BatteryState.Discharging, BatteryScale.FourLevels, 0));
        XInputPad.Battery(type: 3, level: 3).Should().Be(new BatteryReading(BatteryState.Discharging, BatteryScale.FourLevels, 3));
        XInputPad.Battery(type: 3, level: 9).Should().Be(new BatteryReading(BatteryState.Discharging, BatteryScale.FourLevels, 3), "a level past full is full");
        XInputPad.Battery(type: 0, level: 0).Should().BeNull("disconnected");
        XInputPad.Battery(type: 0xFF, level: 2).Should().BeNull("a type XInput itself calls unknown has nothing to show");
    }

    [Fact]
    public void LowIsEmptyOrLowOrTenPerCentAndOnlyOnBattery()
    {
        new BatteryReading(BatteryState.Discharging, BatteryScale.FourLevels, 1).IsLow.Should().BeTrue();
        new BatteryReading(BatteryState.Discharging, BatteryScale.FourLevels, 2).IsLow.Should().BeFalse();
        Percent(10).IsLow.Should().BeTrue();
        Percent(20).IsLow.Should().BeFalse();
        Percent(10, BatteryState.Unknown).IsLow.Should().BeTrue("a level with no state is still a level");
        Percent(10, BatteryState.Charging).IsLow.Should().BeFalse("it is on power");
        BatteryReading.Wired.IsLow.Should().BeFalse();
    }

    [Fact]
    public void TheLongestStateIsTheOneReadForTheMostTime()
    {
        var tally = new BatteryTally();
        tally.Any.Should().BeFalse();
        tally.Add(Percent(80), 600_000);
        tally.Add(Percent(70), 1_500_000);
        tally.Add(Percent(70), 300_000);
        tally.Add(Percent(60), 200_000);
        tally.Add(Percent(60), -5);

        BatterySummary summary = tally.Summary();

        tally.Any.Should().BeTrue();
        summary.Longest.Should().Be(new BatteryStateTime(BatteryState.Discharging, BatteryScale.Percent, 70, 1_800_000));
        summary.States.Select(static s => s.Level).Should().Equal(70, 80, 60);
        summary.First.Should().Be(Percent(80));
        summary.Last.Should().Be(Percent(60));
    }

    [Fact]
    public void AnEmptyTallyHasNoLongestState()
    {
        BatterySummary summary = new BatteryTally().Summary();

        summary.Longest.Should().BeNull();
        summary.First.Should().BeNull();
        summary.DrainPercentPerHour.Should().BeNull();
    }

    [Fact]
    public void TheDrainIsTheFallOverTheTimeOnBattery()
    {
        var tally = new BatteryTally();
        tally.Add(Percent(90), 1_800_000);
        tally.Add(Percent(80), 1_800_000);
        tally.Add(Percent(70), 1_800_000);

        // Twenty points in an hour and a half.
        tally.Summary().DrainPercentPerHour.Should().Be(13.3);
    }

    [Fact]
    public void NoDrainIsWorkedOutFromTooLittleTimeARiseAChargeOrFourLevels()
    {
        var brief = new BatteryTally();
        brief.Add(Percent(90), 200_000);
        brief.Add(Percent(80), 200_000);
        brief.Summary().DrainPercentPerHour.Should().BeNull("under ten minutes, a level that moves in tens says nothing");

        var steady = new BatteryTally();
        steady.Add(Percent(80), 3_600_000);
        steady.Summary().DrainPercentPerHour.Should().BeNull("it did not fall");

        var charged = new BatteryTally();
        charged.Add(Percent(40), 1_200_000);
        charged.Add(Percent(50, BatteryState.Charging), 1_200_000);
        charged.Add(Percent(30), 1_200_000);
        charged.Summary().DrainPercentPerHour.Should().BeNull("it was put on power in between: the fall is not one run");

        var chargedFirst = new BatteryTally();
        chargedFirst.Add(Percent(100, BatteryState.Full), 600_000);
        chargedFirst.Add(Percent(100), 1_800_000);
        chargedFirst.Add(Percent(80), 1_800_000);
        chargedFirst.Summary().DrainPercentPerHour.Should().Be(20, "being on power before it began to run down breaks nothing");

        var levels = new BatteryTally();
        levels.Add(new BatteryReading(BatteryState.Discharging, BatteryScale.FourLevels, 3), 3_600_000);
        levels.Add(new BatteryReading(BatteryState.Discharging, BatteryScale.FourLevels, 1), 3_600_000);
        levels.Summary().DrainPercentPerHour.Should().BeNull("four words are not per cent");
    }
}
