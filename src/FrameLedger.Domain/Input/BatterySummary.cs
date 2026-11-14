// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Input;

/// <summary>What <see cref="BatteryTally"/> adds up to for one controller.</summary>
public sealed record BatterySummary
{
    /// <summary>Every state the battery was read in, the longest first.</summary>
    public IReadOnlyList<BatteryStateTime> States { get; init; } = [];

    /// <summary>The state that lasted longest - the first of <see cref="States"/> - or null when nothing was read.</summary>
    public BatteryStateTime? Longest => States.Count > 0 ? States[0] : null;

    /// <summary>The first reading of the session.</summary>
    public BatteryReading? First { get; init; }

    /// <summary>The last reading of the session.</summary>
    public BatteryReading? Last { get; init; }

    /// <summary>
    /// How fast the battery ran down, in per cent an hour: the fall between the first and the last reading on battery
    /// over the time read on battery. Null unless the level is in per cent, it fell, it was not put on power in
    /// between, and at least <see cref="BatteryTally.MinimumDrainTime"/> was read on battery.
    /// </summary>
    public double? DrainPercentPerHour { get; init; }
}
