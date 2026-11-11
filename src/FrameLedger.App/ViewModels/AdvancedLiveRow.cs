// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>One series as it reads now: its label, its last value in its unit, and when that value was read.</summary>
/// <param name="Label">The catalog entry in this language, with the hardware's name for a member of a family.</param>
/// <param name="Value">The last value read.</param>
/// <param name="ReadAt">The time it was read at - a sensor read every fifth second is as old as this says.</param>
public sealed record AdvancedLiveRow(string Label, string Value, string ReadAt)
{
    /// <summary>What a screen reader says for the row: its label and its value.</summary>
    public override string ToString() => Label + ": " + Value;
}
