// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// What became of the entries read inside the game (<see cref="AdvancedReach.Game"/>) in one session — the third
/// answer beside a value and "not read": why there is none (Advanced metrics train B, owner decision D111).
/// </summary>
public enum AdvancedInGameStatus
{
    /// <summary>Not stated: a row stored before 0.1.0-beta.40, or a state nobody set.</summary>
    Unknown = 0,

    /// <summary>The game made at least one of the calls these entries are read from, and what it passed was read.</summary>
    Reading = 1,

    /// <summary>
    /// The session was measured inside the game and the game made none of those calls where they can be seen — it
    /// uses neither upscaler through the interfaces FrameLedger intercepts, or it sets its options through a call
    /// that cannot be intercepted (<c>slDLSSSetOptions</c>). Not "the game uses none of it".
    /// </summary>
    NothingSeen = 2,

    /// <summary>The session was not measured inside the game (Tier 2): there was no call to read.</summary>
    NotHooked = 3,
}
