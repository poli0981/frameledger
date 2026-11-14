// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Input;

namespace FrameLedger.Domain.Tests.Input;

/// <summary>
/// The three input reports of a DualSense (<c>21_INPUT_DEVICES</c> §DualSense), as bytes written here from the layout:
/// <b>none of them came from a pad</b>. They hold what the reader takes from a report - in use or not, and the battery
/// where the report has one - and that a Bluetooth pad's short report is not read as a USB one.
/// </summary>
public sealed class DualSenseReportTests
{
    private const int _hatNeutral = 0x08;

    /// <summary>A USB report of a pad at rest: sticks centred, hat neutral, nothing down, the battery byte as given.</summary>
    private static byte[] Usb(byte status = 0x08)
    {
        byte[] report = new byte[DualSenseReport.UsbLength];
        report[0] = 0x01;
        report[1] = report[2] = report[3] = report[4] = 128;
        report[8] = _hatNeutral;
        report[53] = status;
        return report;
    }

    /// <summary>The same body behind a Bluetooth extended report's two leading bytes.</summary>
    private static byte[] BluetoothFull(byte status = 0x08)
    {
        byte[] report = new byte[DualSenseReport.BluetoothLength];
        report[0] = 0x31;
        Usb(status).AsSpan(1).CopyTo(report.AsSpan(2));
        return report;
    }

    /// <summary>The short Bluetooth report, padded to the device's longest report as Windows hands it over.</summary>
    private static byte[] BluetoothShort()
    {
        byte[] report = new byte[DualSenseReport.BluetoothLength];
        report[0] = 0x01;
        report[1] = report[2] = report[3] = report[4] = 128;
        report[5] = _hatNeutral;
        return report;
    }

    [Fact]
    public void APadAtRestIsNotInUse()
    {
        DualSenseReport.TryRead(Usb(), bluetooth: false, out bool active, out _).Should().BeTrue();

        active.Should().BeFalse();
    }

    [Theory]
    [InlineData(1, 200)] // left stick, x
    [InlineData(2, 40)] // left stick, y
    [InlineData(3, 255)] // right stick, x
    [InlineData(4, 0)] // right stick, y
    [InlineData(5, 200)] // left trigger
    [InlineData(6, 31)] // right trigger, just past the threshold
    [InlineData(8, 0x00)] // the hat, up
    [InlineData(8, 0x28)] // a face button, the hat neutral
    [InlineData(9, 0x01)] // a shoulder button
    [InlineData(10, 0x01)] // the PS button
    public void AnythingMovedOrHeldIsAPadInUse(int index, byte value)
    {
        byte[] report = Usb();
        report[index] = value;

        DualSenseReport.TryRead(report, bluetooth: false, out bool active, out _).Should().BeTrue();

        active.Should().BeTrue();
    }

    [Theory]
    [InlineData(1, 128 + DualSenseReport.StickDeadzone)] // a stick that drifts inside its dead zone
    [InlineData(5, DualSenseReport.TriggerThreshold)] // a trigger at the threshold, not past it
    [InlineData(7, 0xFF)] // the counter between the triggers and the buttons
    [InlineData(16, 0xFF)] // the gyroscope: a pad lying on a desk still reports it
    [InlineData(33, 0x7F)] // the touchpad
    public void WhatIsNotAButtonAStickOrATriggerIsNotUse(int index, byte value)
    {
        byte[] report = Usb();
        report[index] = value;

        DualSenseReport.TryRead(report, bluetooth: false, out bool active, out _).Should().BeTrue();

        active.Should().BeFalse("drift, the report counter, motion and the touchpad are not the pad being played with");
    }

    [Theory]
    [InlineData(0x08, BatteryState.Discharging, 80)]
    [InlineData(0x00, BatteryState.Discharging, 0)]
    [InlineData(0x0A, BatteryState.Discharging, 100)]
    [InlineData(0x0F, BatteryState.Discharging, 100)] // a level past ten is ten
    [InlineData(0x15, BatteryState.Charging, 50)]
    [InlineData(0x2A, BatteryState.Full, 100)]
    [InlineData(0x20, BatteryState.Full, 100)] // full is 100 whatever the level bits say
    [InlineData(0xA4, BatteryState.Unknown, 40)] // not charging: voltage or temperature
    [InlineData(0xF3, BatteryState.Unknown, 30)] // a charging fault
    public void TheBatteryByteIsALevelInTenthsAndWhatTheBatteryIsDoing(byte status, BatteryState state, int level)
    {
        DualSenseReport.TryRead(Usb(status), bluetooth: false, out _, out BatteryReading? battery).Should().BeTrue();

        battery.Should().Be(new BatteryReading(state, BatteryScale.Percent, level));
    }

    [Fact]
    public void TheExtendedBluetoothReportIsTheUsbBodyOneByteLater()
    {
        byte[] report = BluetoothFull(0x16);
        report[2 + 4] = 255; // the left trigger, where the USB report has it at 5

        DualSenseReport.TryRead(report, bluetooth: true, out bool active, out BatteryReading? battery).Should().BeTrue();

        active.Should().BeTrue();
        battery.Should().Be(new BatteryReading(BatteryState.Charging, BatteryScale.Percent, 60));
    }

    /// <summary>
    /// Windows hands the short report over at the length of the device's longest, with the id the USB report has. Read
    /// as a USB report it would be a pad with an empty battery and its buttons taken for triggers.
    /// </summary>
    [Fact]
    public void TheShortBluetoothReportHasNoBatteryAndIsNotReadAsAUsbOne()
    {
        DualSenseReport.TryRead(BluetoothShort(), bluetooth: true, out bool active, out BatteryReading? battery).Should().BeTrue();

        active.Should().BeFalse();
        battery.Should().BeNull("the short report carries none: over Bluetooth the level is there only once something switched the pad");
    }

    [Theory]
    [InlineData(1, 0)] // a stick
    [InlineData(5, 0x00)] // the hat, up
    [InlineData(6, 0x02)] // a shoulder button
    [InlineData(7, 0x01)] // the PS button
    [InlineData(8, 200)] // the left trigger
    [InlineData(9, 200)] // the right trigger
    public void TheShortBluetoothReportSaysWhenThePadIsInUse(int index, byte value)
    {
        byte[] report = BluetoothShort();
        report[index] = value;

        DualSenseReport.TryRead(report, bluetooth: true, out bool active, out _).Should().BeTrue();

        active.Should().BeTrue();
    }

    [Fact]
    public void TheCounterInTheShortReportsLastButtonByteIsNotAButton()
    {
        byte[] report = BluetoothShort();
        report[7] = 0xFC; // the upper six bits count reports

        DualSenseReport.TryRead(report, bluetooth: true, out bool active, out _).Should().BeTrue();

        active.Should().BeFalse();
    }

    [Fact]
    public void WhatIsNotOneOfTheThreeReportsIsRefused()
    {
        DualSenseReport.TryRead([], bluetooth: false, out _, out _).Should().BeFalse();
        DualSenseReport.TryRead(new byte[20], bluetooth: false, out _, out _).Should().BeFalse("too short for a USB report");
        DualSenseReport.TryRead(BluetoothFull(), bluetooth: false, out _, out _).Should().BeFalse("report 0x31 is not sent over USB");
        DualSenseReport.TryRead([0x31, 0, 0], bluetooth: true, out _, out _).Should().BeFalse("a truncated extended report");
        DualSenseReport.TryRead([0x01, 128, 128], bluetooth: true, out _, out _).Should().BeFalse("a truncated short report");
        DualSenseReport.TryRead(new byte[78], bluetooth: true, out _, out _).Should().BeFalse("an unknown report id");
    }
}
