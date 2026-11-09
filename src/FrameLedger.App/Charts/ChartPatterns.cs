// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using ScottPlot;
using ScottPlot.Plottables;

namespace FrameLedger.App.Charts;

/// <summary>
/// No two series of one chart told apart by colour alone (beta.35, D90; WCAG 2.2 1.4.1): a chart's lines cycle through the
/// line patterns, its point series through the marker shapes, its shaded spans through the hatches — as the latency chart
/// already drew its average dashed and its 95th percentile dotted. A reader who does not tell the palette's red from its
/// green, and a Windows contrast theme with a handful of colours, still tell the series apart. <c>ChartPatternsTests</c>
/// reads every chart's legend for two entries that differ by colour only.
/// </summary>
public static class ChartPatterns
{
    /// <summary>The line patterns, in the order a chart's lines take them.</summary>
    public static IReadOnlyList<LinePattern> Lines { get; } = [LinePattern.Solid, LinePattern.Dashed, LinePattern.Dotted, LinePattern.DenselyDashed];

    /// <summary>The marker shapes, in the order a chart's point series take them; the fifth and later lines carry one too.</summary>
    public static IReadOnlyList<MarkerShape> Markers { get; } = [MarkerShape.FilledCircle, MarkerShape.FilledTriangleUp, MarkerShape.FilledSquare, MarkerShape.FilledDiamond, MarkerShape.OpenCircle];

    /// <summary>The pattern of a chart's <paramref name="index"/>th line, and from the fifth on a marker beside it.</summary>
    public static void Line(Scatter line, int index)
    {
        ArgumentNullException.ThrowIfNull(line);
        line.LinePattern = Lines[index % Lines.Count];
        if (index >= Lines.Count)
        {
            line.MarkerShape = Markers[(index / Lines.Count - 1) % Markers.Count];
            line.MarkerSize = 6;
        }
    }

    /// <inheritdoc cref="Line(Scatter, int)"/>
    public static void Line(SignalXY line, int index)
    {
        ArgumentNullException.ThrowIfNull(line);
        line.LinePattern = Lines[index % Lines.Count];
        if (index >= Lines.Count)
        {
            line.MarkerShape = Markers[(index / Lines.Count - 1) % Markers.Count];
            line.MarkerSize = 6;
        }
    }

    /// <summary>The marker of a chart's <paramref name="index"/>th point series.</summary>
    public static void Points(Scatter points, int index)
    {
        ArgumentNullException.ThrowIfNull(points);
        points.MarkerShape = Markers[index % Markers.Count];
    }

    /// <summary>The hatch of a chart's <paramref name="index"/>th kind of shaded span: stripes, then dots, then a checker.</summary>
    public static IHatch Hatch(int index) => (index % 3) switch
    {
        0 => new ScottPlot.Hatches.Striped(),
        1 => new ScottPlot.Hatches.Dots(),
        _ => new ScottPlot.Hatches.Checker(),
    };
}
