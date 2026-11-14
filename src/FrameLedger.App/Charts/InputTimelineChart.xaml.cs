// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using FrameLedger.App.Services;
using FrameLedger.Domain.Input;
using ScottPlot;
using ScottPlot.Plottables;

namespace FrameLedger.App.Charts;

/// <summary>
/// What each second of a session was played with (0.1.0-beta.50; <see cref="InputSeries"/>): four rows - no input, a
/// controller, the keyboard and mouse, both at once - a mark on a row for every second the game was in front and mostly
/// played that way, and the controllers' connections, drops and low batteries as lines across them. A second with the
/// game not in front has no mark on any row.
/// </summary>
public partial class InputTimelineChart : UserControl
{
    /// <summary>The rows from the bottom: the value a class is stored as is the row it is drawn on.</summary>
    private static readonly InputClass[] _rows = [InputClass.None, InputClass.Controller, InputClass.KeyboardMouse, InputClass.Both];

    public InputTimelineChart()
    {
        InitializeComponent();
        ChartTheme.Attach(Plot);
    }

    /// <summary>The marks on the rows drawn by the last <see cref="Show"/>; 0 when the session stored no input series.</summary>
    public int DrawnPoints { get; private set; }

    /// <summary>The connection, drop and battery lines drawn by the last <see cref="Show"/>.</summary>
    public int DrawnEvents { get; private set; }

    public void Show(InputSeries? series, IReadOnlyList<InputEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        Plot plot = Plot.Plot;
        plot.Clear();
        ChartTheme.Apply(plot);
        plot.XLabel(Strings.Chart_Axis_TimeFromStart);
        DrawnPoints = 0;
        DrawnEvents = 0;
        if (series is { TimesS.Length: > 0 })
        {
            ChartPalette p = ChartTheme.Current;
            for (int r = 0; r < _rows.Length; r++)
            {
                DrawRow(plot, series, _rows[r], p, r);
            }

            double end = series.TimesS[^1];
            DrawnEvents = InputEventLines.Draw(plot, events, p, 0, end);
            plot.Axes.Left.SetTicks([.. _rows.Select(static c => (double)(int)c)], [.. _rows.Select(InputPresentation.ClassLabel)]);

            // A little room on both sides, so the first and the last second are whole marks and not halves on the frame.
            double pad = Math.Max(end * 0.01, 0.25);
            plot.Axes.SetLimits(-pad, Math.Max(end, 1) + pad, -0.5, _rows.Length - 0.5);
            if (DrawnEvents > 0)
            {
                plot.ShowLegend(Alignment.UpperRight);   // the lines' kinds only: the rows are named on the axis
            }
        }

        bool any = DrawnPoints > 0;
        NotRead.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        Plot.Visibility = any ? Visibility.Visible : Visibility.Hidden;
        Plot.Refresh();
    }

    private void DrawRow(Plot plot, InputSeries series, InputClass input, ChartPalette palette, int index)
    {
        int[] ticks = [.. Enumerable.Range(0, series.Class.Length).Where(i => !float.IsNaN(series.Class[i]) && (int)series.Class[i] == (int)input)];
        if (ticks.Length == 0)
        {
            return;
        }

        DrawnPoints += ticks.Length;
        double[] xs = [.. ticks.Select(i => series.TimesS[i])];
        double[] ys = [.. ticks.Select(_ => (double)(int)input)];
        Scatter row = plot.Add.Scatter(xs, ys, palette.InputFill(input));
        row.LineWidth = 0;
        row.MarkerSize = 5;

        // No legend entry: the axis names the row, and a legend of seven would cover a chart this low.
        ChartPatterns.Points(row, index);
    }
}
