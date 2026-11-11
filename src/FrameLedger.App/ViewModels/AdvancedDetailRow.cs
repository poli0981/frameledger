// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>One row under a table-valued fact of the Advanced page: a driver setting, a memory module.</summary>
/// <param name="Name">What the row is, in the words it was stored with.</param>
/// <param name="Value">Its value, a token of it in this language.</param>
public sealed record AdvancedDetailRow(string Name, string Value)
{
    /// <summary>What a screen reader says for the row: its name and its value.</summary>
    public override string ToString() => Name + ": " + Value;
}
