// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>One line of the game page's Details card (beta.8): a label and what the game's files — or its store — say.</summary>
public sealed record GameDetailRow(string Label, string Value)
{
    /// <summary>The line as a screen reader reads it (beta.35, D88): <c>Runs as: Direct3D 12</c>.</summary>
    public override string ToString() => Services.SpokenText.Pair(Label, Value);
}
