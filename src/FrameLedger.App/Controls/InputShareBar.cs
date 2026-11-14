// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using FrameLedger.App.Charts;

namespace FrameLedger.App.Controls;

/// <summary>
/// What a session was played with as one bar (0.1.0-beta.50, <c>08_UI</c> §Session summary): each segment a class's
/// share of the time the game was in front, in the chart palette's colours (<see cref="ChartPalette.InputFill"/>). The
/// table beside it carries every number in text; the drawing is <see cref="ShareBar"/>'s.
/// </summary>
public sealed class InputShareBar : ShareBar
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments), typeof(IReadOnlyList<InputShareSegment>), typeof(InputShareBar),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<InputShareSegment>? Segments
    {
        get => (IReadOnlyList<InputShareSegment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    protected override IReadOnlyList<double> Shares => Segments is { Count: > 0 } segments ? [.. segments.Select(static s => s.Share)] : [];

    protected override ScottPlot.Color Fill(ChartPalette palette, int index)
    {
        ArgumentNullException.ThrowIfNull(palette);
        return palette.InputFill(Segments![index].Class);
    }
}
