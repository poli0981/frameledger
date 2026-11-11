// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>One stored series of a session in numbers: how many readings, and their lowest, average and highest.</summary>
public sealed record AdvancedStatRow(string Label, string Samples, string Min, string Mean, string Max)
{
    /// <summary>What a screen reader says for the row: its label and its average.</summary>
    public override string ToString() => Label + ": " + Mean;
}
