// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using FrameLedger.Application.Persistence;
using ScottPlot;
using ScottPlot.Plottables;

namespace FrameLedger.App.Charts;

/// <summary>
/// The render scale over a session (beta.14, D49; <see cref="RenderScaleSeries"/>): a step line — a scale holds until the next
/// change — and, where the upscaler's parameters were never measured, a sentence saying so instead of an empty plot.
/// </summary>
public partial class RenderScaleChart : UserControl
{
    public RenderScaleChart()
    {
        InitializeComponent();
        ChartTheme.Attach(Plot);
    }

    /// <summary>The points drawn by the last <see cref="Show"/>; 0 when the scale was not measured.</summary>
    public int DrawnPoints { get; private set; }

    public void Show(SessionSeries? series, SessionRow? row)
    {
        ScottPlot.Plot plot = Plot.Plot;
        plot.Clear();
        ChartTheme.Apply(plot);
        plot.XLabel(Strings.Chart_Axis_TimeFromFirstFrame);
        plot.YLabel(Strings.Chart_Axis_RenderScale);
        (double[] xs, double[] ys) = row is null ? ([], []) : RenderScaleSeries.Of(series, row);
        DrawnPoints = xs.Length;
        if (xs.Length > 0)
        {
            Scatter line = plot.Add.Scatter(xs, ys, ChartTheme.Current.Native);
            line.ConnectStyle = ConnectStyle.StepHorizontal;
            line.MarkerSize = 0;
            line.LineWidth = 2;
            plot.Axes.AutoScale();

            // A scale reads against 0 and 100 %: an axis fitted to one flat line would make 66.7 % look like a cliff.
            plot.Axes.SetLimitsY(0, Math.Max(100, ys.Max() * 1.05));
        }

        NotMeasured.Visibility = xs.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
        Plot.Visibility = xs.Length > 0 ? Visibility.Visible : Visibility.Hidden;
        Plot.Refresh();
    }
}
