// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Consent;

/// <summary>
/// Which operator-facing surface is showing <see cref="OperatorDisclosure"/>: the text's first line and the
/// provenance it stamps differ, and nothing else does.
/// </summary>
public enum OperatorSurface
{
    /// <summary>The unshipped capture host's <c>consent grant</c> (P0, 2026-08-06).</summary>
    UnshippedHost = 0,

    /// <summary>The Agent's <c>--console consent grant</c> (P2 PR-F, HANDOFF §P2 decision D4).</summary>
    AgentConsole = 1,
}
