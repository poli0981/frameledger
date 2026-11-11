// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>The facts of one group of the catalog - the graphics card, the displays - under its heading.</summary>
/// <param name="Title">The group in this language.</param>
/// <param name="Rows">Its facts, in the catalog's order.</param>
public sealed record AdvancedFactGroup(string Title, IReadOnlyList<AdvancedFactRow> Rows)
{
    /// <summary>What a screen reader says for the group: its title.</summary>
    public override string ToString() => Title;
}
