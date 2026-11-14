// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Input;

namespace FrameLedger.Domain.Tests.Input;

/// <summary>
/// The two facts taken from the name Windows gives a controller's device interface (<c>21_INPUT_DEVICES</c> §What is
/// never read). The names here have the shape Windows writes; their instance parts are made up.
/// </summary>
public sealed class RawDeviceNameTests
{
    private const string _xboxUsb = @"\\?\HID#VID_045E&PID_0B12&IG_00#8&1a2b3c4d&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const string _xboxBluetooth = @"\\?\HID#{00001812-0000-1000-8000-00805f9b34fb}&Dev&VID_045e&PID_0b13&REV_0520&IG_00#9&2b3c4d5e&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const string _dualSenseUsb = @"\\?\HID#VID_054C&PID_0CE6&MI_03#8&3c4d5e6f&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const string _dualSenseBluetooth = @"\\?\HID#{00001124-0000-1000-8000-00805F9B34FB}_VID&0002054c_PID&0ce6#8&4d5e6f70&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";

    [Theory]
    [InlineData(_xboxUsb, true)]
    [InlineData(_xboxBluetooth, true)]
    [InlineData(@"\\?\hid#vid_28de&pid_11ff&ig_00#7&5e6f7081&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", true)]
    [InlineData(_dualSenseUsb, false)]
    [InlineData(_dualSenseBluetooth, false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ADeviceWindowsPresentsThroughXInputCarriesMicrosoftsMarkInItsName(string? name, bool xinput) =>
        RawDeviceName.IsXInputClass(name).Should().Be(xinput);

    [Theory]
    [InlineData(_xboxUsb, ControllerConnection.Usb)]
    [InlineData(_dualSenseUsb, ControllerConnection.Usb)]
    [InlineData(_dualSenseBluetooth, ControllerConnection.Bluetooth)] // classic Bluetooth's HID service
    [InlineData(_xboxBluetooth, ControllerConnection.Bluetooth)] // low energy's, which also carries VID_
    [InlineData(@"\\?\HID#SOMETHING_ELSE#1&2&3#{4d1e55b2-f16f-11cf-88cb-001111000030}", ControllerConnection.Unknown)]
    [InlineData("", ControllerConnection.Unknown)]
    [InlineData(null, ControllerConnection.Unknown)]
    public void TheConnectionIsBluetoothByTheServicesIdentifierAndUsbByAUsbVendorId(string? name, ControllerConnection connection) =>
        RawDeviceName.ConnectionOf(name).Should().Be(connection);
}
