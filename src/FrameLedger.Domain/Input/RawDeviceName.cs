// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Input;

/// <summary>
/// The two things FrameLedger takes from the name Windows gives a controller's device interface, after which the name
/// is dropped (<c>21_INPUT_DEVICES</c> §What is never read): whether Windows presents the device through XInput, and
/// how it is connected.
/// </summary>
/// <remarks>
/// <para>
/// The name is a path such as <c>\\?\HID#VID_045E&amp;PID_0B12&amp;IG_00#…</c>. On Bluetooth it carries the service's
/// identifier - and, further along, an instance id that on some stacks is derived from the device's address. That is
/// why the name is never kept, logged or stored: this type returns two facts and holds no string.
/// </para>
/// <para>
/// <b><c>IG_</c></b> is Microsoft's documented mark of a device XInput presents ("XInput and DirectInput": a device id
/// that contains <c>IG_</c> is an XInput device). <b>The connection</b> is Bluetooth when the name carries the
/// identifier of the Bluetooth HID service, classic (<c>00001124-…</c>) or low energy (<c>00001812-…</c>), and USB
/// when it carries a USB vendor id written <c>VID_</c>; anything else is not known.
/// </para>
/// </remarks>
public static class RawDeviceName
{
    private const string _xinputMark = "IG_";
    private const string _bluetoothClassicService = "{00001124-0000-1000-8000-00805f9b34fb}";
    private const string _bluetoothLowEnergyService = "{00001812-0000-1000-8000-00805f9b34fb}";
    private const string _usbVendor = "VID_";

    /// <summary>Whether Windows presents the device through XInput.</summary>
    public static bool IsXInputClass(string? name) => name is not null && name.Contains(_xinputMark, StringComparison.OrdinalIgnoreCase);

    /// <summary>How the device is connected, as far as its name says.</summary>
    public static ControllerConnection ConnectionOf(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return ControllerConnection.Unknown;
        }

        if (name.Contains(_bluetoothClassicService, StringComparison.OrdinalIgnoreCase) || name.Contains(_bluetoothLowEnergyService, StringComparison.OrdinalIgnoreCase))
        {
            return ControllerConnection.Bluetooth;
        }

        return name.Contains(_usbVendor, StringComparison.OrdinalIgnoreCase) ? ControllerConnection.Usb : ControllerConnection.Unknown;
    }
}
