// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using FrameLedger.App.Charts;

namespace FrameLedger.App.Controls;

/// <summary>
/// The frame-generation mix as one bar (beta.24, owner decision D62): each segment a row's share of the session's presenting
/// time, in the chart palette's colours (<see cref="ChartPalette.MixFill"/>). It says nothing the table beside it does not —
/// every number is there, in text — and is announced by its <c>AutomationProperties.Name</c>. The drawing is
/// <see cref="ShareBar"/>'s.
/// </summary>
public sealed class FgMixBar : ShareBar
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments), typeof(IReadOnlyList<FgMixSegment>), typeof(FgMixBar),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<FgMixSegment>? Segments
    {
        get => (IReadOnlyList<FgMixSegment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    protected override IReadOnlyList<double> Shares => Segments is { Count: > 0 } segments ? [.. segments.Select(static s => s.Share)] : [];

    /// <summary>Each segment's rectangle in a bar <paramref name="width"/> by <paramref name="height"/> (<see cref="ShareBar.Layout"/>).</summary>
    internal static IReadOnlyList<Rect> Layout(IReadOnlyList<FgMixSegment> segments, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(segments);
        return Layout([.. segments.Select(static s => s.Share)], width, height);
    }

    protected override ScottPlot.Color Fill(ChartPalette palette, int index)
    {
        ArgumentNullException.ThrowIfNull(palette);
        FgMixSegment segment = Segments![index];
        return palette.MixFill(segment.Kind, segment.Factor);
    }
}
