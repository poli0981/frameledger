// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Input;
using FrameLedger.Domain.Input;

namespace FrameLedger.Application.Tests.Input;

/// <summary>
/// One session's slices added up into a reading a tick (<c>03_METRICS</c> §Input): only the time the game was in front
/// is classified, a pause is not observed, and a controller that was there at the start has not connected.
/// </summary>
public sealed class InputTickAccumulatorTests
{
    private static readonly InputDevice _xbox = new() { Id = 1, Family = ControllerFamily.Xbox, XInputSlot = 0 };

    private static readonly InputDevice _dualSense = new()
    {
        Id = 2,
        Family = ControllerFamily.PlayStation,
        VendorId = ControllerCatalog.Sony,
        ProductId = ControllerCatalog.DualSense,
        Connection = ControllerConnection.Usb,
    };

    private static InputSlice Slice(bool keyboardMouse = false, InputHealth? health = null, bool virtualPad = false, params InputSliceDevice[] devices) => new()
    {
        ElapsedMs = 100,
        ForegroundProcessId = 42,
        KeyboardMouseActive = keyboardMouse,
        VirtualPad = virtualPad,
        Devices = devices,
        Health = health ?? new InputHealth(),
    };

    private static InputSliceDevice Pad(InputDevice device, bool active = false, BatteryReading? battery = null) => new(device, active, battery);

    [Fact]
    public void NothingAddedIsNothingToTake() =>
        new InputTickAccumulator().Take().Should().BeNull();

    [Fact]
    public void EachSliceInFrontCountsForOneOfTheFourClasses()
    {
        var accumulator = new InputTickAccumulator();
        accumulator.Add(Slice(devices: Pad(_xbox, active: true)), inFront: true, paused: false);
        accumulator.Add(Slice(devices: Pad(_xbox, active: true)), inFront: true, paused: false);
        accumulator.Add(Slice(keyboardMouse: true, devices: Pad(_xbox)), inFront: true, paused: false);
        accumulator.Add(Slice(keyboardMouse: true, devices: Pad(_xbox, active: true)), inFront: true, paused: false);
        accumulator.Add(Slice(devices: Pad(_xbox)), inFront: true, paused: false);

        InputReading reading = accumulator.Take()!;

        reading.SpanMs.Should().Be(500);
        reading.ObservedMs.Should().Be(500);
        reading.ForegroundMs.Should().Be(500);
        reading.ControllerMs.Should().Be(200);
        reading.KeyboardMouseMs.Should().Be(100);
        reading.BothMs.Should().Be(100);
        reading.IdleMs.Should().Be(100);
        reading.Devices.Should().Equal(new InputDeviceReading(1, Connected: true, ActiveMs: 300, Battery: null, Connects: 0, Disconnects: 0));
        reading.Table.Should().Equal(_xbox);
    }

    /// <summary>Typing in a browser beside the game is not the game played with the keyboard.</summary>
    [Fact]
    public void ASliceWithAnotherProgramInFrontIsNotClassified()
    {
        var accumulator = new InputTickAccumulator();
        accumulator.Add(Slice(keyboardMouse: true, devices: Pad(_xbox, active: true)), inFront: false, paused: false);
        accumulator.Add(Slice(devices: Pad(_xbox, active: true)), inFront: true, paused: false);

        InputReading reading = accumulator.Take()!;

        reading.SpanMs.Should().Be(200);
        reading.ObservedMs.Should().Be(200);
        reading.ForegroundMs.Should().Be(100);
        reading.ControllerMs.Should().Be(100);
        reading.KeyboardMouseMs.Should().Be(0);
        reading.BothMs.Should().Be(0);
        reading.Devices[0].ActiveMs.Should().Be(100, "a controller's time in use is time in the game");
    }

    [Fact]
    public void APausedSliceIsNotObservedButItsTimeAndItsControllersAre()
    {
        var accumulator = new InputTickAccumulator();
        accumulator.Add(Slice(devices: Pad(_xbox, active: true)), inFront: true, paused: true);
        accumulator.Add(Slice(), inFront: true, paused: true);

        InputReading reading = accumulator.Take()!;

        reading.SpanMs.Should().Be(200, "a battery level is timed by the clock, paused or not");
        reading.ObservedMs.Should().Be(0);
        reading.ForegroundMs.Should().Be(0);
        reading.ControllerMs.Should().Be(0);
        reading.Devices.Should().Equal(new InputDeviceReading(1, Connected: false, ActiveMs: 0, Battery: null, Connects: 0, Disconnects: 1));
    }

    [Fact]
    public void AControllerThereAtTheStartHasNotConnectedAndOneThatComesLaterHas()
    {
        var accumulator = new InputTickAccumulator();
        accumulator.Add(Slice(devices: Pad(_xbox)), inFront: true, paused: false);
        accumulator.Add(Slice(devices: [Pad(_xbox), Pad(_dualSense)]), inFront: true, paused: false);

        InputReading reading = accumulator.Take()!;

        reading.Devices.Should().Equal(
            new InputDeviceReading(1, Connected: true, ActiveMs: 0, Battery: null, Connects: 0, Disconnects: 0),
            new InputDeviceReading(2, Connected: true, ActiveMs: 0, Battery: null, Connects: 1, Disconnects: 0));
        reading.Table.Should().Equal(_xbox, _dualSense);
    }

    [Fact]
    public void ADropAndAReturnAreCountedEvenAcrossTicks()
    {
        var accumulator = new InputTickAccumulator();
        accumulator.Add(Slice(devices: Pad(_dualSense)), inFront: true, paused: false);
        accumulator.Add(Slice(), inFront: true, paused: false);
        InputReading dropped = accumulator.Take()!;
        accumulator.Add(Slice(), inFront: true, paused: false);
        InputReading gone = accumulator.Take()!;
        accumulator.Add(Slice(devices: Pad(_dualSense)), inFront: true, paused: false);
        InputReading back = accumulator.Take()!;

        dropped.Devices.Should().Equal(new InputDeviceReading(2, Connected: false, ActiveMs: 0, Battery: null, Connects: 0, Disconnects: 1));
        gone.Devices.Should().BeEmpty("a controller that left is not carried into the next tick");
        gone.Table.Should().BeEmpty();
        back.Devices.Should().Equal(new InputDeviceReading(2, Connected: true, ActiveMs: 0, Battery: null, Connects: 1, Disconnects: 0));
    }

    [Fact]
    public void ATakeStartsTheNextTickFromNothingButTheControllersStillConnected()
    {
        var accumulator = new InputTickAccumulator();
        accumulator.Add(Slice(keyboardMouse: true, virtualPad: true, devices: Pad(_xbox, active: true)), inFront: true, paused: false);
        accumulator.Take().Should().NotBeNull();
        accumulator.Take().Should().BeNull("no slice since the last take");

        accumulator.Add(Slice(devices: Pad(_xbox)), inFront: true, paused: false);
        InputReading next = accumulator.Take()!;

        next.SpanMs.Should().Be(100);
        next.BothMs.Should().Be(0);
        next.IdleMs.Should().Be(100);
        next.VirtualPad.Should().BeFalse();
        next.Devices.Should().Equal(new InputDeviceReading(1, Connected: true, ActiveMs: 0, Battery: null, Connects: 0, Disconnects: 0));
    }

    [Fact]
    public void TheBatteryIsTheLastOneReportedAndASliceWithNoneKeepsIt()
    {
        var eighty = new BatteryReading(BatteryState.Discharging, BatteryScale.Percent, 80);
        var seventy = new BatteryReading(BatteryState.Discharging, BatteryScale.Percent, 70);
        var accumulator = new InputTickAccumulator();
        accumulator.Add(Slice(devices: Pad(_dualSense, battery: eighty)), inFront: true, paused: false);
        accumulator.Add(Slice(devices: Pad(_dualSense, battery: seventy)), inFront: true, paused: false);
        accumulator.Add(Slice(devices: Pad(_dualSense)), inFront: true, paused: false);

        accumulator.Take()!.Devices[0].Battery.Should().Be(seventy);

        accumulator.Add(Slice(devices: Pad(_dualSense)), inFront: true, paused: false);
        accumulator.Take()!.Devices[0].Battery.Should().Be(seventy, "the level a pad last reported is still its level");
    }

    [Fact]
    public void AVirtualPadSeenInAnySliceMarksTheTick()
    {
        var accumulator = new InputTickAccumulator();
        accumulator.Add(Slice(), inFront: true, paused: false);
        accumulator.Add(Slice(virtualPad: true), inFront: false, paused: true);

        accumulator.Take()!.VirtualPad.Should().BeTrue();
    }

    [Fact]
    public void TheSameControllerNamedTwiceInASliceCountsOnce()
    {
        var accumulator = new InputTickAccumulator();
        accumulator.Add(Slice(devices: [Pad(_xbox, active: true), Pad(_xbox, active: true)]), inFront: true, paused: false);

        accumulator.Take()!.Devices.Should().Equal(new InputDeviceReading(1, Connected: true, ActiveMs: 100, Battery: null, Connects: 0, Disconnects: 0));
    }

    /// <summary>The reader runs before and after a session: a session's counts start where it began to read.</summary>
    [Fact]
    public void TheHealthIsWhatHappenedSinceTheSessionsFirstSlice()
    {
        var accumulator = new InputTickAccumulator();
        accumulator.Add(Slice(health: new InputHealth { RawInputRegistered = true, RawReports = 1000, Slices = 50, ThreadCpuMs = 7, XInputReads = 40 }), inFront: true, paused: false);
        accumulator.Add(Slice(health: new InputHealth { RawInputRegistered = true, XInputAvailable = true, RawReports = 1250, RawReportsUnread = 2, Slices = 51, ThreadCpuMs = 9, XInputReads = 44, XInputSilent = 1, Faults = 1 }), inFront: true, paused: false);

        InputHealth health = accumulator.Take()!.Health;

        health.Should().Be(new InputHealth
        {
            RawInputRegistered = true,
            XInputAvailable = true,
            RawReports = 250,
            RawReportsUnread = 2,
            Slices = 1,
            ThreadCpuMs = 2,
            XInputReads = 4,
            XInputSilent = 1,
            Faults = 1,
        });
        health.Sources().Should().Be("rawinput+xinput");
    }

    [Fact]
    public void TheSourcesAreTheOnesThatAnswered()
    {
        new InputHealth().Sources().Should().BeEmpty();
        new InputHealth { LastInputAvailable = true }.Sources().Should().Be("lastinput");
        new InputHealth { RawInputRegistered = true, XInputAvailable = true, LastInputAvailable = true }.Sources().Should().Be("rawinput+xinput+lastinput");
    }

    [Fact]
    public void ANegativeElapsedTimeCountsForNothing()
    {
        var accumulator = new InputTickAccumulator();
        accumulator.Add(Slice(devices: Pad(_xbox, active: true)) with { ElapsedMs = -40 }, inFront: true, paused: false);

        InputReading reading = accumulator.Take()!;

        reading.SpanMs.Should().Be(0);
        reading.ControllerMs.Should().Be(0);
    }

    [Fact]
    public void TheStoredWordsReadBackAsWhatTheyWere()
    {
        foreach (ControllerFamily family in Enum.GetValues<ControllerFamily>())
        {
            InputNames.FamilyOf(InputNames.Of(family)).Should().Be(family);
        }

        foreach (ControllerConnection connection in Enum.GetValues<ControllerConnection>())
        {
            InputNames.ConnectionOf(InputNames.Of(connection)).Should().Be(connection);
        }

        foreach (BatteryState state in Enum.GetValues<BatteryState>())
        {
            InputNames.StateOf(InputNames.Of(state)).Should().Be(state);
        }

        foreach (BatteryScale scale in Enum.GetValues<BatteryScale>())
        {
            InputNames.ScaleOf(InputNames.Of(scale)).Should().Be(scale);
        }

        InputNames.FamilyOf("a word a later release wrote").Should().Be(ControllerFamily.Other);
        InputNames.ConnectionOf(null).Should().Be(ControllerConnection.Unknown);
        InputNames.StateOf(null).Should().Be(BatteryState.Unknown);
        InputNames.ScaleOf(null).Should().Be(BatteryScale.None);
    }

    [Fact]
    public void ADevicesModelIsItsCatalogName()
    {
        _dualSense.Model.Should().Be("DualSense");
        _xbox.Model.Should().BeNull("one of XInput's pads that nothing named has no ids to look up");
    }
}
