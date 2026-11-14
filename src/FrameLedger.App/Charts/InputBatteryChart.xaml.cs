// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using FrameLedger.Application.Input;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Input;
using ScottPlot;
using ScottPlot.Plottables;

namespace FrameLedger.App.Charts;

/// <summary>
/// Each controller's battery over a session (0.1.0-beta.50; <see cref="InputSeries"/>): a step line a controller - a
/// level holds until the next reading - on one axis of per cent. A controller that reports four levels (XInput's empty,
/// low, medium, full) is drawn at thirds and its legend says so: it is a coarser scale, not a measured percentage.
/// </summary>
public partial class InputBatteryChart : UserControl
{
    public InputBatteryChart()
    {
        InitializeComponent();
        ChartTheme.Attach(Plot);
    }

    /// <summary>The lines drawn by the last <see cref="Show"/>; 0 when no controller reported a battery.</summary>
    public int DrawnLines { get; private set; }

    public void Show(InputSeries? series, InputDevicesRecord? record)
    {
        Plot plot = Plot.Plot;
        plot.Clear();
        ChartTheme.Apply(plot);
        plot.XLabel(Strings.Chart_Axis_TimeFromStart);
        plot.YLabel(Strings.Chart_Axis_Battery);
        DrawnLines = 0;
        if (series is not null)
        {
            ChartPalette p = ChartTheme.Current;
            Color[] colours = [p.Native, p.Displayed, p.Sensor, p.SensorSecondary, p.Percentile, p.SensorTertiary];
            foreach (InputDeviceSeries device in series.Devices.Where(static d => d.Battery is not null))
            {
                BatteryScale scale = InputNames.ScaleOf(record?.Devices.FirstOrDefault(d => d.Index == device.Index)?.Battery?.Scale);
                DrawDevice(plot, series, device, scale, colours[DrawnLines % colours.Length]);
            }

            if (DrawnLines > 0)
            {
                // The axis runs past 100 so the legend has a place no level can be drawn under.
                plot.Axes.Left.SetTicks([0, 25, 50, 75, 100], ["0", "25", "50", "75", "100"]);
                plot.Axes.SetLimits(0, Math.Max(series.TimesS[^1], 1), 0, 135);
                plot.ShowLegend(Alignment.UpperRight);
            }
        }

        NotRead.Visibility = DrawnLines > 0 ? Visibility.Collapsed : Visibility.Visible;
        Plot.Visibility = DrawnLines > 0 ? Visibility.Visible : Visibility.Hidden;
        Plot.Refresh();
    }

    private void DrawDevice(Plot plot, InputSeries series, InputDeviceSeries device, BatteryScale scale, Color colour)
    {
        float[] battery = device.Battery!;
        int[] read = [.. Enumerable.Range(0, battery.Length).Where(i => !float.IsNaN(battery[i]))];
        if (read.Length == 0 || scale == BatteryScale.None)
        {
            return;
        }

        double[] xs = [.. read.Select(i => series.TimesS[i])];
        double[] ys = [.. read.Select(i => InputSeries.ToPercent(battery[i], scale))];
        Scatter line = plot.Add.Scatter(xs, ys, colour);
        line.ConnectStyle = ConnectStyle.StepHorizontal;
        line.MarkerSize = 0;
        line.LineWidth = 2;
        line.LegendText = scale == BatteryScale.FourLevels
            ? InputSeries.DeviceLabel(device.Index) + " " + Strings.Chart_Input_FourLevels
            : InputSeries.DeviceLabel(device.Index);
        ChartPatterns.Line(line, DrawnLines);
        DrawnLines++;
    }
}
