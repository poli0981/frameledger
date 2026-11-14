// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Input;

/// <summary>What a controller's battery was doing at one reading.</summary>
public enum BatteryState
{
    /// <summary>The controller reported a battery and not what it was doing, or reported a fault.</summary>
    Unknown = 0,

    /// <summary>Running on its battery.</summary>
    Discharging,

    /// <summary>Charging.</summary>
    Charging,

    /// <summary>Charged and on power.</summary>
    Full,

    /// <summary>A wired controller with no battery to report (XInput's word).</summary>
    Wired,
}
