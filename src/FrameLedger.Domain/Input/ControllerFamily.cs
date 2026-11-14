// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Input;

/// <summary>
/// Whose controller a device is, from its USB vendor id (<c>03_METRICS</c> §Input). <see cref="Xbox"/> is also what
/// Windows presents through XInput with no vendor id: a pad of another maker that says it is an Xbox pad is one here,
/// because that is all Windows tells.
/// </summary>
public enum ControllerFamily
{
    /// <summary>A game controller of a maker this build has no name for.</summary>
    Other = 0,

    /// <summary>Microsoft's, and every pad Windows presents through XInput with no vendor id.</summary>
    Xbox,

    /// <summary>Sony's: DualSense, DualSense Edge, DualShock 4.</summary>
    PlayStation,

    /// <summary>Nintendo's.</summary>
    Nintendo,
}
