// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;
using FrameLedger.Domain.Input;

namespace FrameLedger.Application.Input;

/// <summary>One controller during one tick.</summary>
/// <param name="DeviceId">The <see cref="InputDevice.Id"/> it is about.</param>
/// <param name="Connected">Whether it was connected at the end of the tick.</param>
/// <param name="ActiveMs">The time it was in use while the game was in front and the session was not paused.</param>
/// <param name="Battery">Its battery at the tick's last reading, when it reported one.</param>
/// <param name="Connects">How many times it connected during the tick. Being there when the session began is not a connection.</param>
/// <param name="Disconnects">How many times it disconnected during the tick.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct InputDeviceReading(int DeviceId, bool Connected, int ActiveMs, BatteryReading? Battery, int Connects, int Disconnects);
