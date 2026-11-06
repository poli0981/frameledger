// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>A two-way filter's three choices (beta.34, D85): everything, only the ones that are, only the ones that are not.</summary>
public enum YesNoFilter
{
    All = 0,
    Yes,
    No,
}
