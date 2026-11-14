// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;
using FrameLedger.Domain.Input;

namespace FrameLedger.Application.Input;

/// <summary>
/// A game controller as Windows' raw input describes it, reduced to what the reader keeps: its two USB ids, whether
/// Windows presents it through XInput, and how it is connected. The name these were read from is not here - it can
/// carry an address - and nothing else of the device is asked for.
/// </summary>
/// <param name="VendorId">The USB vendor id.</param>
/// <param name="ProductId">The USB product id.</param>
/// <param name="XInputClass">Whether Windows presents the device through XInput (<see cref="RawDeviceName.IsXInputClass"/>).</param>
/// <param name="Connection">How it is connected (<see cref="RawDeviceName.ConnectionOf"/>).</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct RawController(int VendorId, int ProductId, bool XInputClass, ControllerConnection Connection)
{
    public bool IsVirtual => ControllerCatalog.IsVirtual(VendorId, ProductId);

    public bool IsDualSense => !XInputClass && ControllerCatalog.IsDualSense(VendorId, ProductId);
}
