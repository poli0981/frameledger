// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>One row of Compare's stat table: the metric and one cell per compared session, in the selection's order.</summary>
public sealed record CompareRowViewModel(string Metric, IReadOnlyList<CompareCell> Cells);
