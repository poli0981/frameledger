// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>One entry of the catalog on the Sources tab: what it is, what it belongs to, where it comes from, and whether it is read - or why not.</summary>
/// <param name="Label">The entry in this language.</param>
/// <param name="Scope">This PC, a session, or a session over time.</param>
/// <param name="Source">The interface it comes from.</param>
/// <param name="Status">Read, or the reason it is not - in words, never by colour alone.</param>
/// <param name="IsRead">Whether the Agent reads it at all.</param>
public sealed record AdvancedSourceRow(string Label, string Scope, string Source, string Status, bool IsRead)
{
    /// <summary>What a screen reader says for the row: its label and whether it is read, or why not.</summary>
    public override string ToString() => Label + ": " + Status;
}
