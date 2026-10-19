// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.App.Controls;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// One row of the summary's frame-generation mix (beta.24, D62): what the row is, its share of the presenting time, its own
/// Native → Displayed (CLAUDE.md rule 6: a state's factor never stands without them), and how long it lasted — from
/// <c>sessions.fg_mix</c>, never recomputed.
/// </summary>
/// <param name="Label"><c>×4.0 frame generation</c>, <c>No frame generation</c> or <c>Not classified</c>.</param>
/// <param name="Share">Two decimals, as the owner's example reads (<c>35.33%</c>); <c>&lt;0.01%</c> for a sliver.</param>
/// <param name="Rates"><c>70 → 282 FPS</c> for a state, <c>118 FPS</c> with no frame generation, empty where no state was read.</param>
/// <param name="Duration">The time the row covers.</param>
/// <param name="Segment">The row as the bar draws it.</param>
public sealed record FgMixRowModel(string Label, string Share, string Rates, string Duration, FgMixSegment Segment)
{
    /// <summary>The row's colour, as a one-segment bar beside its label.</summary>
    public IReadOnlyList<FgMixSegment> Swatch { get; } = [Segment with { Share = 1 }];
}
