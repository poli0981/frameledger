// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Application.Input;

/// <summary>What XInput says of one of its pads beside its state: whether it is wireless, and its battery in XInput's own words.</summary>
/// <param name="Wireless"><c>XINPUT_CAPS_WIRELESS</c>.</param>
/// <param name="BatteryType"><c>XINPUT_BATTERY_INFORMATION.BatteryType</c>; 0 (disconnected) when XInput did not answer.</param>
/// <param name="BatteryLevel"><c>XINPUT_BATTERY_INFORMATION.BatteryLevel</c>.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct XInputDetails(bool Wireless, int BatteryType, int BatteryLevel);
