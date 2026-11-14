// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Input;
using FrameLedger.Domain.Input;
using FrameLedger.Testing;

namespace FrameLedger.Application.Tests.Input;

/// <summary>
/// The input reader over a Windows whose every answer the test writes (<c>21_INPUT_DEVICES</c> §The reader): what a
/// controller is, when it is in use, what stands in for XInput where XInput says nothing, and what the reader says of
/// itself. <b>No device is behind any of it</b> - what a real controller answers is owner-only item 48.
/// </summary>
public sealed class InputReaderTests
{
    private const nint _deviceA = 0x1001;
    private const nint _deviceB = 0x1002;

    private static readonly RawController _xboxBluetooth = new(ControllerCatalog.Microsoft, 0x0B13, XInputClass: true, ControllerConnection.Bluetooth);
    private static readonly RawController _xboxUsb = new(ControllerCatalog.Microsoft, 0x0B12, XInputClass: true, ControllerConnection.Usb);
    private static readonly RawController _steamPad = new(ControllerCatalog.Valve, ControllerCatalog.SteamVirtualGamepad, XInputClass: true, ControllerConnection.Usb);
    private static readonly RawController _dualSenseUsb = new(ControllerCatalog.Sony, ControllerCatalog.DualSense, XInputClass: false, ControllerConnection.Usb);
    private static readonly RawController _dualSenseBluetooth = _dualSenseUsb with { Connection = ControllerConnection.Bluetooth };
    private static readonly RawController _dualShock = new(ControllerCatalog.Sony, 0x09CC, XInputClass: false, ControllerConnection.Usb);

    private static readonly XInputReading _atRest = new(Packet: 7, 0, 0, 0, 0, 0, 0, 0);
    private static readonly XInputReading _buttonDown = _atRest with { Packet = 8, Buttons = 0x1000 };

    private static (InputReader Reader, FakeInputBackend Backend) Make()
    {
        var backend = new FakeInputBackend();
        var reader = new InputReader(backend);
        reader.Open();
        return (reader, backend);
    }

    /// <summary>A USB report of a DualSense: at rest, or with a button down; the battery byte as given.</summary>
    private static byte[] DualSenseUsb(bool pressed = false, byte status = 0x08)
    {
        byte[] report = new byte[DualSenseReport.UsbLength];
        report[0] = 0x01;
        report[1] = report[2] = report[3] = report[4] = 128;
        report[8] = pressed ? (byte)0x28 : (byte)0x08;
        report[53] = status;
        return report;
    }

    [Fact]
    public void WithNothingConnectedASliceHasNoControllerAndSaysWhatAnswered()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        backend.CpuMs = 3;

        InputSlice slice = reader.Slice(104);

        backend.Opened.Should().BeTrue();
        slice.ElapsedMs.Should().Be(104);
        slice.ForegroundProcessId.Should().Be(42);
        slice.Devices.Should().BeEmpty();
        slice.KeyboardMouseActive.Should().BeFalse("the first slice has no time before it to compare with");
        slice.VirtualPad.Should().BeFalse();
        slice.Health.Should().Be(new InputHealth { RawInputRegistered = true, XInputAvailable = true, LastInputAvailable = true, Slices = 1, ThreadCpuMs = 3 });
    }

    [Fact]
    public void TheOnlyDevicesRawInputIsRegisteredForAreJoysticksAndGamepads() =>
        InputReader.RegisteredUsages.Should().Equal([(1, 4), (1, 5)], "usage 2 is a mouse and 6 a keyboard: neither is ever registered for (D158)");

    /// <summary>XInput looks for a device each time it is asked about an empty slot, so an empty slot is asked about rarely.</summary>
    [Fact]
    public void AnEmptySlotIsAskedAboutOnceInTwentySlicesAndAConnectedOneEverySlice()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        backend.Pads[1] = _atRest;

        for (int i = 0; i < InputReader.ProbeEverySlices; i++)
        {
            reader.Slice(100);
        }

        backend.PadAsks.Should().Equal(1, InputReader.ProbeEverySlices, 1, 1);
        reader.Slice(100);
        backend.PadAsks.Should().Equal(2, InputReader.ProbeEverySlices + 1, 2, 2);
    }

    [Fact]
    public void AnXInputPadIsAControllerOfTheXboxFamilyAndIsInUseWhenItsStateSaysSo()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        backend.Pads[2] = _buttonDown;

        InputSliceDevice pad = reader.Slice(100).Devices.Should().ContainSingle().Subject;

        pad.Device.Should().Be(new InputDevice { Id = 3, Family = ControllerFamily.Xbox, XInputSlot = 2 });
        pad.Device.Model.Should().BeNull("XInput does not say which device a pad is, and nothing else named it");
        pad.Active.Should().BeTrue();
        pad.Battery.Should().BeNull("XInput was not asked, or did not answer");

        backend.Pads[2] = _atRest with { ThumbLx = 5000, ThumbLy = 5000 };
        reader.Slice(100).Devices[0].Active.Should().BeFalse("a stick inside its dead zone is drift");

        backend.Pads.Remove(2);
        reader.Slice(100).Devices.Should().BeEmpty("XInput says it is not connected any more");
    }

    [Fact]
    public void APadsBatteryAndWirelessFlagAreAskedForWhenItConnectsAndThenOnceInFiftySlices()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        backend.Pads[0] = _atRest;
        backend.Details[0] = new XInputDetails(Wireless: true, BatteryType: 3, BatteryLevel: 2);

        InputSliceDevice pad = reader.Slice(100).Devices[0];

        pad.Battery.Should().Be(new BatteryReading(BatteryState.Discharging, BatteryScale.FourLevels, 2));
        pad.Device.Connection.Should().Be(ControllerConnection.Wireless);
        backend.DetailAsks.Should().Be(1);

        backend.Details[0] = new XInputDetails(Wireless: false, BatteryType: 1, BatteryLevel: 0);
        for (int i = 0; i < InputReader.DetailsEverySlices - 1; i++)
        {
            reader.Slice(100);
        }

        backend.DetailAsks.Should().Be(1, "not yet");
        pad = reader.Slice(100).Devices[0];
        backend.DetailAsks.Should().Be(2);
        pad.Battery.Should().Be(BatteryReading.Wired);
        pad.Device.Connection.Should().Be(ControllerConnection.Usb, "the pad is wired now, and its description follows");
    }

    [Fact]
    public void APadIsNamedWhenTheOnlyXInputClassDeviceThereCanBeIt()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        backend.Pads[0] = _atRest;
        backend.Details[0] = new XInputDetails(Wireless: true, BatteryType: 0, BatteryLevel: 0);
        backend.Arrive(_deviceA, _xboxBluetooth);

        InputSlice slice = reader.Slice(100);

        InputSliceDevice pad = slice.Devices.Should().ContainSingle("the device raw input named IS the pad, not a second controller").Subject;
        pad.Device.Should().Be(new InputDevice
        {
            Id = 1,
            Family = ControllerFamily.Xbox,
            VendorId = ControllerCatalog.Microsoft,
            ProductId = 0x0B13,
            Connection = ControllerConnection.Bluetooth,
            XInputSlot = 0,
        });
        pad.Device.Model.Should().Be("Xbox Wireless Controller");
        pad.Battery.Should().BeNull("type 0 is XInput's own word for nothing to show - a pad on Bluetooth is reported not to answer");
        slice.VirtualPad.Should().BeFalse();
    }

    [Fact]
    public void TwoDifferentXInputClassDevicesLeaveThePadsUnnamed()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        backend.Pads[0] = _atRest;
        backend.Pads[1] = _atRest;
        backend.Arrive(_deviceA, _xboxBluetooth);
        backend.Arrive(_deviceB, _xboxUsb);

        IReadOnlyList<InputSliceDevice> pads = reader.Slice(100).Devices;

        pads.Should().HaveCount(2);
        pads.Should().OnlyContain(static p => p.Device.VendorId == 0 && p.Device.Family == ControllerFamily.Xbox, "which pad is which device cannot be told, and a wrong name is worse than none");

        backend.Leave(_deviceB);
        reader.Slice(100).Devices.Should().OnlyContain(static p => p.Device.ProductId == 0x0B13, "one device left: both pads can only be it");
    }

    [Fact]
    public void SteamsVirtualGamepadMarksTheSliceAndNamesThePadsOnlyWhenNothingElseCan()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        backend.Pads[0] = _atRest;
        backend.Arrive(_deviceA, _steamPad);

        InputSlice alone = reader.Slice(100);

        alone.VirtualPad.Should().BeTrue();
        alone.Devices.Should().ContainSingle().Which.Device.Should().BeEquivalentTo(new InputDevice
        {
            Id = 1,
            Family = ControllerFamily.Xbox,
            VendorId = ControllerCatalog.Valve,
            ProductId = ControllerCatalog.SteamVirtualGamepad,
            IsVirtual = true,
            XInputSlot = 0,
        });

        backend.Arrive(_deviceB, _xboxUsb);
        InputSlice beside = reader.Slice(100);

        beside.VirtualPad.Should().BeTrue();
        beside.Devices[0].Device.IsVirtual.Should().BeFalse("with a pad a player holds beside it, the pads take that pad's name");
        beside.Devices[0].Device.ProductId.Should().Be(0x0B12);
    }

    [Fact]
    public void ADualSenseIsReadFromItsOwnReports()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        backend.Arrive(_deviceA, _dualSenseUsb);
        backend.Send(_deviceA, DualSenseUsb());
        backend.Send(_deviceA, DualSenseUsb(pressed: true, status: 0x07));
        backend.Send(_deviceA, DualSenseUsb(status: 0x06));

        InputSlice slice = reader.Slice(100);

        InputSliceDevice pad = slice.Devices.Should().ContainSingle().Subject;
        pad.Device.Should().Be(new InputDevice { Id = 16, Family = ControllerFamily.PlayStation, VendorId = ControllerCatalog.Sony, ProductId = ControllerCatalog.DualSense, Connection = ControllerConnection.Usb });
        pad.Active.Should().BeTrue("one of the slice's reports was a pad in use");
        pad.Battery.Should().Be(new BatteryReading(BatteryState.Discharging, BatteryScale.Percent, 60), "the last report's");
        slice.Health.RawReports.Should().Be(3);
        slice.Health.RawReportsUnread.Should().Be(0);

        InputSliceDevice later = reader.Slice(100).Devices[0];
        later.Active.Should().BeFalse("no report in this slice said so");
        later.Battery.Should().Be(pad.Battery, "the level a pad last reported is still its level");
    }

    [Fact]
    public void ADualSenseOnBluetoothIsReadByItsShortReportUntilSomethingElseSwitchesIt()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        byte[] shortReport = new byte[DualSenseReport.BluetoothLength];
        shortReport[0] = 0x01;
        shortReport[1] = shortReport[2] = shortReport[3] = shortReport[4] = 128;
        shortReport[5] = 0x08;
        shortReport[8] = 200;
        backend.Arrive(_deviceA, _dualSenseBluetooth);
        backend.Send(_deviceA, shortReport);

        InputSliceDevice pad = reader.Slice(100).Devices[0];

        pad.Active.Should().BeTrue();
        pad.Battery.Should().BeNull("the short report has none, and the reader never asks the pad for the long one (D159)");
        pad.Device.Connection.Should().Be(ControllerConnection.Bluetooth);
    }

    [Fact]
    public void AReportTheParserCannotReadAndAReportOfADeviceNobodyNamedAreCountedAsNotRead()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        backend.Arrive(_deviceA, _dualSenseUsb);
        backend.Send(_deviceA, 0x7F, 1, 2, 3);
        backend.Send(_deviceB, DualSenseUsb(pressed: true));

        InputSlice slice = reader.Slice(100);

        slice.Health.RawReports.Should().Be(2);
        slice.Health.RawReportsUnread.Should().Be(2);
        slice.Devices.Should().ContainSingle().Which.Active.Should().BeFalse();
    }

    /// <summary>A controller whose layout this build does not know is listed, with its connections and drops, and is never "in use": a guess would be a made-up number.</summary>
    [Fact]
    public void AControllerWhoseReportsThisBuildCannotReadIsListedAndNeverInUse()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        backend.Arrive(_deviceA, _dualShock);
        backend.Send(_deviceA, 1, 255, 255, 255, 255, 255, 255, 255);

        InputSlice slice = reader.Slice(100);

        InputSliceDevice pad = slice.Devices.Should().ContainSingle().Subject;
        pad.Device.Family.Should().Be(ControllerFamily.PlayStation);
        pad.Device.Model.Should().Be("DualShock 4");
        pad.Active.Should().BeFalse();
        pad.Battery.Should().BeNull();
        slice.Health.RawReportsUnread.Should().Be(0, "it is not a report the reader set out to read");
    }

    [Fact]
    public void AControllerKeepsItsNumberAcrossAReconnectAndASecondOfTheSameModelHasItsOwn()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        backend.Arrive(_deviceA, _dualSenseUsb);
        backend.Arrive(_deviceA, _dualSenseUsb);
        reader.Slice(100).Devices.Select(static d => d.Device.Id).Should().Equal([16], "Windows naming a device twice is one device");

        backend.Leave(_deviceA);
        reader.Slice(100).Devices.Should().BeEmpty();

        backend.Arrive(_deviceB, _dualSenseUsb);
        reader.Slice(100).Devices.Select(static d => d.Device.Id).Should().Equal([16], "the same model on the same connection, back under another handle");

        backend.Arrive(_deviceA, _dualSenseUsb);
        backend.Arrive(0x1003, _dualSenseBluetooth);
        reader.Slice(100).Devices.Select(static d => d.Device.Id).Should().Equal(16, 17, 18);

        backend.Leave(0x4242);
        reader.Slice(100).Devices.Should().HaveCount(3, "a device nobody named leaving changes nothing");
    }

    [Fact]
    public void TheKeyboardOrMouseWasInUseWhenWindowsTimeOfTheLastInputMoved()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        reader.Slice(100).KeyboardMouseActive.Should().BeFalse();
        reader.Slice(100).KeyboardMouseActive.Should().BeFalse("the time did not move");

        backend.LastInputTick = 1090;
        reader.Slice(100).KeyboardMouseActive.Should().BeTrue();
        reader.Slice(100).KeyboardMouseActive.Should().BeFalse();

        backend.LastInputTick = 900;
        reader.Slice(100).KeyboardMouseActive.Should().BeTrue("Windows does not promise the time only goes forward; a different one is input all the same");
    }

    [Fact]
    public void WhenWindowsDoesNotGiveTheTimeOfTheLastInputNothingIsSaidOfTheKeyboardAndTheHealthSaysSo()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        backend.LastInputTick = null;

        InputSlice slice = reader.Slice(100);

        slice.KeyboardMouseActive.Should().BeFalse();
        slice.Health.LastInputAvailable.Should().BeFalse();
        slice.Health.Sources().Should().Be("rawinput+xinput");
    }

    [Fact]
    public void WithoutXInputsLibraryNoPadIsAskedAbout()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        backend.XInputAvailable = false;
        backend.Pads[0] = _buttonDown;

        InputSlice slice = reader.Slice(100);

        slice.Devices.Should().BeEmpty();
        slice.Health.XInputAvailable.Should().BeFalse();
        backend.PadAsks.Should().Equal(0, 0, 0, 0);
    }

    /// <summary>
    /// Microsoft's page for XInputEnable says the system switches a program's controller input by its window's focus.
    /// Where that leaves the Agent with a pad that reads at rest for ever, the device's own reports stand in.
    /// </summary>
    [Fact]
    public void WhereXInputSaysNothingWhileTheDevicesReportsChangeThePadCountsAsInUseAfterTenSuchSlices()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        backend.Pads[1] = _atRest;
        backend.Arrive(_deviceA, _xboxUsb);
        backend.Send(_deviceA, 0, 1);
        reader.Slice(100).Devices[0].Active.Should().BeFalse("the first report has nothing before it to differ from");

        for (int i = 1; i < InputReader.SilentBeforeFallback; i++)
        {
            backend.Send(_deviceA, 0, (byte)(i + 1));
            InputSlice early = reader.Slice(100);
            early.Devices[0].Active.Should().BeFalse($"silent slice {i}: not enough of them yet");
            early.Health.XInputSilent.Should().Be(i);
        }

        backend.Send(_deviceA, 0, 200);
        InputSlice slice = reader.Slice(100);

        slice.Health.XInputSilent.Should().Be(InputReader.SilentBeforeFallback);
        slice.Devices.Should().ContainSingle().Which.Active.Should().BeTrue("XInput has never once been seen to move, and the device keeps saying something new");

        backend.Send(_deviceA, 0, 200);
        reader.Slice(100).Devices[0].Active.Should().BeFalse("the same report again is a pad at rest");
    }

    [Fact]
    public void WhereXInputHasBeenSeenToMoveTheDevicesReportsNeverStandInForIt()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        backend.Pads[0] = _atRest;
        backend.Arrive(_deviceA, _xboxUsb);
        backend.Send(_deviceA, 0, 1);
        reader.Slice(100);

        // XInput moves: it answers this program. The slice it moved in and the one after are not silent ones.
        backend.Pads[0] = _atRest with { Packet = 9, ThumbLx = 3000 };
        backend.Send(_deviceA, 0, 2);
        reader.Slice(100).Health.XInputSilent.Should().Be(0);
        backend.Send(_deviceA, 0, 3);
        reader.Slice(100).Health.XInputSilent.Should().Be(0, "the report may be the one XInput already answered for");

        // From here the device's reports change - a stick that drifts - and XInput's packet stays: counted, never used.
        for (int i = 0; i < InputReader.SilentBeforeFallback + 5; i++)
        {
            backend.Send(_deviceA, 0, (byte)(10 + i));
            reader.Slice(100).Devices[0].Active.Should().BeFalse("XInput is the one to say, and it says at rest");
        }

        reader.Slice(100).Health.XInputSilent.Should().Be(InputReader.SilentBeforeFallback + 5);
    }

    [Fact]
    public void ASliceInWhichXInputSaysThePadIsInUseIsNotASilentOne()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        backend.Pads[0] = _buttonDown;
        backend.Arrive(_deviceA, _xboxUsb);
        backend.Send(_deviceA, 0, 1);
        reader.Slice(100);
        backend.Send(_deviceA, 0, 2);

        InputSlice slice = reader.Slice(100);

        slice.Health.XInputSilent.Should().Be(0, "a button held gives the same packet number and a pad in use");
        slice.Devices[0].Active.Should().BeTrue();
    }

    [Fact]
    public void AFaultIsCountedAndANegativeElapsedTimeIsNone()
    {
        (InputReader reader, FakeInputBackend backend) = Make();
        backend.ThrowOnDrain = true;
        Action slice = () => reader.Slice(100);
        slice.Should().Throw<InvalidOperationException>("the reader does not hide a failure; the hub counts it");
        reader.Fault();
        backend.ThrowOnDrain = false;

        InputSlice after = reader.Slice(-5);

        after.ElapsedMs.Should().Be(0);
        after.Health.Faults.Should().Be(1);
        after.Health.Slices.Should().Be(2);
    }

    [Fact]
    public void ARegistrationWindowsRefusedIsSaidInEverySlice()
    {
        using var backend = new FakeInputBackend { Registers = false };
        var reader = new InputReader(backend);
        reader.Open();

        InputHealth health = reader.Slice(100).Health;

        health.RawInputRegistered.Should().BeFalse();
        health.Sources().Should().Be("xinput+lastinput");
    }

    [Fact]
    public void WhatADescriptionSaysOfADevice()
    {
        _steamPad.IsVirtual.Should().BeTrue();
        _xboxUsb.IsVirtual.Should().BeFalse();
        _dualSenseBluetooth.IsDualSense.Should().BeTrue();
        _dualShock.IsDualSense.Should().BeFalse();
        (_dualSenseUsb with { XInputClass = true }).IsDualSense.Should().BeFalse("presented through XInput, it is one of XInput's pads");
    }
}
