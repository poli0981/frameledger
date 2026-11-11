// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>What an advanced metric's value is, so one formatter can print it.</summary>
public enum AdvancedUnit
{
    /// <summary>Not stated. No entry of the catalog carries it.</summary>
    None = 0,

    /// <summary>Words — a name, a version, a list of flags.</summary>
    Text = 1,

    /// <summary>Several rows, each a name and a value — a driver profile, the memory modules.</summary>
    Table = 2,

    /// <summary>Yes or no.</summary>
    Flag = 3,

    /// <summary>A whole number with no unit.</summary>
    Count = 4,

    Celsius = 5,
    Percent = 6,
    Megahertz = 7,
    Mebibytes = 8,
    Watts = 9,
    Volts = 10,
    Amperes = 11,
    Rpm = 12,
    Microseconds = 13,
    Milliseconds = 14,
    Nits = 15,
    FramesPerSecond = 16,
    BytesPerSecond = 17,
    Bits = 18,
}
