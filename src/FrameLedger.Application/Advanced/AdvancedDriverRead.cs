// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// What became, in one session, of the one thing FrameLedger reads inside a game that is not an argument of a call
/// it hooks: the graphics driver's latency report, which the part inside the game asks NVIDIA's driver for itself,
/// about once a second (Advanced metrics train C; owner decisions D132, D136, D138-D141; the entries of reach
/// <see cref="AdvancedReach.GameDriver"/>). Beside <see cref="AdvancedInGameCall"/> because the reasons a value is
/// absent are different ones: there is no hook to be in or out, and there is a driver that can refuse.
/// </summary>
public enum AdvancedDriverRead
{
    /// <summary>Not stated: a row stored before this existed, or a session the part inside the game said nothing about.</summary>
    Unknown = 0,

    /// <summary>The driver was asked and reported frames.</summary>
    Read = 1,

    /// <summary>The driver was asked and reported no frame: the game does not use NVIDIA Reflex.</summary>
    NothingReported = 2,

    /// <summary>The game does not present through Direct3D 12, the one interface this is read on.</summary>
    NoDirect3D12 = 3,

    /// <summary>The game has not loaded NVIDIA's <c>nvapi64.dll</c>, and FrameLedger never loads it.</summary>
    NoModule = 4,

    /// <summary>The <c>nvapi64.dll</c> in the game sits in the game's own folder: a replacement, not the driver's (D140).</summary>
    Refused = 5,

    /// <summary>The module did not answer for the two functions.</summary>
    NoGetter = 6,

    /// <summary>The driver answered every read with an error.</summary>
    Error = 7,

    /// <summary>One call took two milliseconds or more: the read was switched off until the game exited (D141).</summary>
    Slow = 8,

    /// <summary>A call into the driver faulted: the read was switched off until the game exited.</summary>
    Faulted = 9,

    /// <summary>A call into the driver did not return.</summary>
    Stalled = 10,
}
