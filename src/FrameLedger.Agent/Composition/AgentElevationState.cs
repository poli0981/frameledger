// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Agent.Composition;

/// <summary>
/// The admin mode's answer at this Agent's start (beta.10, owner decision D34): registered by <c>--serve</c> alone, read into
/// <c>HelloAck.ElevationOutcome</c>. Null when nobody asked — the option off, or elevation inherited from whoever started it.
/// </summary>
/// <param name="Outcome">One of <c>Infrastructure.Startup.AgentElevation</c>'s outcomes, or null.</param>
internal sealed record AgentElevationState(string? Outcome);
