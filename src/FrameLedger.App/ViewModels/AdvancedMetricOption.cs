// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>A catalog entry the chart can draw, in the selector: its id and its label in this language.</summary>
public sealed record AdvancedMetricOption(string Id, string Label)
{
    public override string ToString() => Label;
}
