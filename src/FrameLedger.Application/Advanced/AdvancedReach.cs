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

    /// <summary>Needs a hook on a function its module exports; the hook is not built. No entry carries it since 0.1.0-beta.45.</summary>
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

    /// <summary>
    /// Read inside a game FrameLedger measures, from the arguments of a call the Overlay hooks, while the session
    /// records Advanced metrics (train B, owner decision D111) — and only when the game makes that call.
    /// </summary>
    Game = 12,

    /// <summary>
    /// The game makes the call once, before FrameLedger is inside it: no shipped mode injects before a game's first
    /// instruction, and no hook is ever installed on a game's thread. Never read (owner decision D129).
    /// </summary>
    BeforeAttach = 13,

    /// <summary>
    /// Read inside a game FrameLedger measures, while the session records Advanced metrics — but not from a call the
    /// game makes: the part inside the game asks the graphics driver itself, about once a second, for what the driver
    /// reports (Advanced metrics train C; owner decisions D132, D136, D138). Only where the game presents through
    /// Direct3D 12 and had already loaded the driver's library; <see cref="AdvancedDriverRead"/> says what became of
    /// it in a session.
    /// </summary>
    GameDriver = 14,

    /// <summary>
    /// Reading it needs a route the project refused, whatever the source: a patch at an address learned at run time
    /// (what a game asks of Reflex is an argument of a function no module exports), or a call the vendor's own header
    /// forbids from a thread that is not the game's (owner decision D133). Never read.
    /// </summary>
    RefusedRoute = 15,
}
