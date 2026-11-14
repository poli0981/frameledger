// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.App.Controls;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// One row of the summary's "Played with" table (0.1.0-beta.50): a class of input, its share of the time the game was
/// in front and how long that was - from the row's stored totals, never recomputed from the series.
/// </summary>
/// <param name="Label"><c>Controller</c>, <c>Keyboard and mouse</c>, <c>Both at once</c> or <c>No input</c>.</param>
/// <param name="Share">One decimal (<c>83.2%</c>).</param>
/// <param name="Duration">The time the class covers.</param>
/// <param name="Segment">The row as the bar draws it.</param>
public sealed record InputClassRowModel(string Label, string Share, string Duration, InputShareSegment Segment)
{
    /// <summary>The row's colour, as a one-segment bar beside its label.</summary>
    public IReadOnlyList<InputShareSegment> Swatch { get; } = [Segment with { Share = 1 }];

    /// <summary>The row in one line, as a screen reader reads it.</summary>
    public override string ToString() => Services.SpokenText.Pair(Label, Services.SpokenText.List(Share, Duration));
}
