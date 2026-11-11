// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// Where an advanced metric can be read from — or why it is not read. The catalog names what is NOT read beside what
/// is, so the page can say so with the reason instead of leaving a gap (<c>docs/18_GPU_VENDOR_APIS.md</c> §Advanced
/// metrics; rule 7's "N/A, never a guess" applied to a whole source).
/// </summary>
public enum AdvancedReach
{
    /// <summary>Not stated. No entry of the catalog carries it.</summary>
    None = 0,

    /// <summary>The Agent reads it in its own process, unelevated.</summary>
    Agent = 1,

    /// <summary>The Agent reads it only when it already runs as administrator and PawnIO is installed (D103).</summary>
    AgentElevated = 2,

    /// <summary>An argument of a call the Overlay already hooks; not read yet.</summary>
    HookedCall = 3,

    /// <summary>Needs a hook on a function its module exports; the hook is not built.</summary>
    NewHook = 4,

    /// <summary>A route nobody has proven safe: a spike decides, and the owner says go or no.</summary>
    Spike = 5,

    /// <summary>A source the project decided not to open (D103).</summary>
    Refused = 6,

    /// <summary>A source left for later, with no decision against it.</summary>
    Deferred = 7,

    /// <summary>It identifies a device, a person or another program. Never read (D104).</summary>
    Identifier = 8,

    /// <summary>It is the game's own state — where the player is, where the camera looks. Never read (CLAUDE.md rule 4).</summary>
    PlayerState = 9,

    /// <summary>The vendor's licence forbids vendoring and re-declaring what reading it needs.</summary>
    Licence = 10,

    /// <summary>The vendor's public headers do not declare it.</summary>
    NotInHeaders = 11,
}
