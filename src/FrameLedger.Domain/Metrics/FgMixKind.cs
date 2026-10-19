// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>What one row of a session's frame-generation mix (<see cref="FgMix"/>) is.</summary>
public enum FgMixKind
{
    /// <summary>A frame-generation state: windows agreeing within <see cref="FgSteadyState.StateTolerance"/>.</summary>
    Generated,

    /// <summary>Windows that counted no generated frame (a factor at or below <see cref="FgWindow.NoneCeiling"/>).</summary>
    None,

    /// <summary>
    /// Time no state can claim: a window with too few frames or no application frame counted, a factor between
    /// <see cref="FgWindow.NoneCeiling"/> and <see cref="FgWindow.ActiveThreshold"/>, a factor changing faster than the
    /// windows (a transition, or dynamic frame generation), a group of agreeing windows shorter than
    /// <see cref="FgSteadyState.MinSeconds"/>, and — where a counted <c>none</c> may not be trusted — the windows that
    /// read none.
    /// </summary>
    Unclassified,
}
