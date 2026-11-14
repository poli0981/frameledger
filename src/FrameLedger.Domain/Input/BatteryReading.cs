// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Domain.Input;

/// <summary>A controller's battery at one reading: what it was doing and how full it was, in the source's own steps.</summary>
/// <param name="State">What the battery was doing.</param>
/// <param name="Scale">What <paramref name="Level"/> counts in.</param>
/// <param name="Level">The level in <paramref name="Scale"/>; 0 when the scale is <see cref="BatteryScale.None"/>.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct BatteryReading(BatteryState State, BatteryScale Scale, int Level)
{
    /// <summary>A wired controller: no battery.</summary>
    public static BatteryReading Wired { get; } = new(BatteryState.Wired, BatteryScale.None, 0);

    /// <summary>
    /// Whether a player would call the level low while the controller runs on its battery: XInput's "empty" or "low",
    /// or 10 % and under. Never while it charges, and never for a reading with no level.
    /// </summary>
    public bool IsLow => State is BatteryState.Discharging or BatteryState.Unknown && Scale switch
    {
        BatteryScale.FourLevels => Level <= 1,
        BatteryScale.Percent => Level <= 10,
        _ => false,
    };
}
