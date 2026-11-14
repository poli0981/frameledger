// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;
using FrameLedger.Domain.Input;

namespace FrameLedger.Application.Input;

/// <summary>One connected controller during one slice.</summary>
/// <param name="Device">What it is.</param>
/// <param name="Active">Whether anything on it was in use during the slice.</param>
/// <param name="Battery">Its battery, when it reported one.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct InputSliceDevice(InputDevice Device, bool Active, BatteryReading? Battery);
