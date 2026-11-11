// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>One stat card of the session summary: a label and its value as text (N/A where the tier has none).</summary>
public sealed record StatCardModel(string Label, string Value, string? Suffix = null)
{
    /// <summary>The card as a screen reader reads it (beta.35, D88): <c>Average: 62 FPS</c>.</summary>
    public override string ToString() => Services.SpokenText.Pair(Label, string.IsNullOrEmpty(Suffix) ? Value : Value + " " + Suffix);
}
