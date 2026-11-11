// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Controls;
using ScottPlot;
using ScottPlot.Plottables;

namespace FrameLedger.App.Charts;

/// <summary>
/// The Advanced page's chart (owner decision D101): the series of one catalog entry over time. <see cref="TimeOfDay"/>
/// says what the horizontal axis is — the clock, for what the PC reads now, or seconds from a session's start — and is
/// set once, before the first <see cref="Show"/>: a date axis replaces the bottom axis and is not swapped back.
/// </summary>
/// <remarks>
/// Lines differ by pattern and marker as well as by colour (<see cref="ChartPatterns"/>), so a chart read without
/// colour still tells them apart; and a series with one reading is a point, not an invisible line.
/// </remarks>
public partial class AdvancedSeriesChart : UserControl
{
    private bool _dateAxis;

    public AdvancedSeriesChart()
    {
        InitializeComponent();
        ChartTheme.Attach(Plot);
    }

    /// <summary>True when the points' x values are OLE automation dates (a live chart); false for seconds from a session's start.</summary>
    public bool TimeOfDay { get; set; }

    public Plot ScottPlot => Plot.Plot;

    /// <summary>How many lines the last <see cref="Show"/> drew — what a test reads.</summary>
    public int DrawnLines { get; private set; }

    /// <summary>Draws <paramref name="lines"/> against <paramref name="unit"/>; no line clears the chart.</summary>
    public void Show(IReadOnlyList<AdvancedChartLine> lines, string unit)
    {
        ArgumentNullException.ThrowIfNull(lines);
        Plot plot = Plot.Plot;
        plot.Clear();
        if (TimeOfDay && !_dateAxis)
        {
            // Before the theme and the labels: the date axis REPLACES the bottom axis and would drop both (TrendChart).
            plot.Axes.DateTimeTicksBottom();
            _dateAxis = true;
        }

        ChartTheme.Apply(plot);
        plot.XLabel(TimeOfDay ? Strings.Advanced_Chart_Time : Strings.Advanced_Chart_Seconds);
        plot.YLabel(unit ?? string.Empty);

        ChartPalette p = ChartTheme.Current;
        Color[] palette = [p.Native, p.Displayed, p.Percentile, p.Sensor, p.SensorSecondary, p.Stutter, p.Histogram, p.Segment];
        int drawn = 0;
        foreach (AdvancedChartLine line in lines)
        {
            if (line.Xs.Length == 0 || line.Xs.Length != line.Ys.Length)
            {
                continue;
            }

            Scatter scatter = plot.Add.Scatter(line.Xs, line.Ys, palette[drawn % palette.Length]);
            scatter.LineWidth = 2;
            scatter.MarkerSize = line.Xs.Length == 1 ? 7 : 0;
            scatter.LegendText = line.Label;
            ChartPatterns.Line(scatter, drawn);
            if (line.Xs.Length == 1)
            {
                ChartPatterns.Points(scatter, drawn);
            }

            drawn++;
        }

        DrawnLines = drawn;
        if (drawn > 0)
        {
            plot.Axes.AutoScale();
            plot.ShowLegend(Alignment.UpperLeft);
        }
        else
        {
            plot.HideLegend();
        }

        Plot.Refresh();
    }
}
