// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Input;

/// <summary>What a <see cref="BatteryReading.Level"/> counts in: each source keeps its own steps, none is converted into another's.</summary>
public enum BatteryScale
{
    /// <summary>No level: a wired controller, or one that reported a state only.</summary>
    None = 0,

    /// <summary>XInput's four words: 0 empty, 1 low, 2 medium, 3 full.</summary>
    FourLevels,

    /// <summary>Per cent, 0 to 100 - a DualSense reports it in steps of ten.</summary>
    Percent,
}
