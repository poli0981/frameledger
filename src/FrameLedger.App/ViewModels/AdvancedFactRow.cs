// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>One fact of the Advanced page: what it is, its value in one line, and the rows of a table-valued one.</summary>
/// <param name="Label">The catalog entry in this language, with the display or device it is about.</param>
/// <param name="Value">The value in its unit; for a table, how many rows it has.</param>
/// <param name="Details">The rows of a table-valued fact; empty for every other.</param>
public sealed record AdvancedFactRow(string Label, string Value, IReadOnlyList<AdvancedDetailRow> Details)
{
    /// <summary>Whether there are rows to unfold.</summary>
    public bool HasDetails => Details.Count > 0;

    /// <summary>What a screen reader says for the row: its label and its value.</summary>
    public override string ToString() => Label + ": " + Value;
}
