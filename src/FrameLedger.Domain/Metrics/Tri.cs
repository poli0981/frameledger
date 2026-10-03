// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>
/// CLAUDE.md rule 7's tri-state. Zero is N/A, so a value nobody set is not a claim.
/// </summary>
public enum Tri
{
    NotApplicable = 0,
    No,
    Yes,
}
