// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>Compare's tier filter (beta.34, D85): every session, the measured (Tier 1), or those recorded without measurement.</summary>
public enum CompareTierFilter
{
    All = 0,
    Measured,
    NotMeasured,
}
