// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using ScottPlot;
using ScottPlot.Plottables;

namespace FrameLedger.App.Charts;

/// <summary>
/// A session's controller marks as vertical lines on a chart (0.1.0-beta.50): a connection, a drop, a battery that fell
/// to low. Each kind has a line pattern of its own as well as a colour, and one legend entry - nothing is told by colour
/// alone (<c>08_UI</c> §Accessibility).
/// </summary>
public static class InputEventLines
{
    /// <summary>
    /// Draws the marks that fall on the chart's axis and returns how many it drew.
    /// </summary>
    /// <param name="plot">The chart.</param>
    /// <param name="events">The session's marks, on the sensors' clock.</param>
    /// <param name="palette">The theme's colours.</param>
    /// <param name="offsetS">Where the chart's zero is on that clock: 0 for a chart from the session's start, the first present's time for one over the frames.</param>
    /// <param name="endS">The end of the chart's axis; a mark after it, or before zero, is left out.</param>
    public static int Draw(Plot plot, IReadOnlyList<InputEvent> events, ChartPalette palette, double offsetS, double endS)
    {
        ArgumentNullException.ThrowIfNull(plot);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(palette);
        var named = new HashSet<InputEventKind>();
        int drawn = 0;
        foreach (InputEvent mark in events)
        {
            double x = mark.TimeS - offsetS;
            if (x < 0 || x > endS)
            {
                continue;
            }

            VerticalLine line = plot.Add.VerticalLine(x);
            line.LineWidth = 1;
            line.Color = ColorOf(mark.Kind, palette);
            line.LinePattern = PatternOf(mark.Kind);

            // One legend entry a kind: the first line of each carries it.
            line.LegendText = named.Add(mark.Kind) ? InputSeries.Describe(mark.Kind) : string.Empty;
            drawn++;
        }

        return drawn;
    }

    /// <summary>A kind's colour: series colours, never the stutter reds - a mark is a fact, not a fault.</summary>
    public static Color ColorOf(InputEventKind kind, ChartPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        return kind switch
        {
            InputEventKind.Connected => palette.Sensor,
            InputEventKind.Disconnected => palette.SensorSecondary,
            _ => palette.Percentile,
        };
    }

    /// <summary>A kind's line pattern: dashed, dotted, densely dashed - solid is the frames'.</summary>
    public static LinePattern PatternOf(InputEventKind kind) => kind switch
    {
        InputEventKind.Connected => LinePattern.Dashed,
        InputEventKind.Disconnected => LinePattern.Dotted,
        _ => LinePattern.DenselyDashed,
    };
}
