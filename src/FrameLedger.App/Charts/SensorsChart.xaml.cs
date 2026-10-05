// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using FrameLedger.App.Services;
using FrameLedger.Application.Recording;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WPF;

namespace FrameLedger.App.Charts;

/// <summary>
/// The Sensors tab: GPU core / hotspot temperature, load, CPU temperature and load on one plot (°C and % left) with the GPU's
/// power on a watt axis at the right; then video memory and memory, each with THIS GAME's own figure beside the whole
/// graphics card's or the whole PC's as differently-labelled series, in GB (beta.12, D43: the game's figures are read from
/// outside the game once a second, in either tier). Since beta.8 it draws a session that was not hooked too: its sensors are
/// all it has.
/// </summary>
/// <remarks>
/// Corrected 2026-10-03: the memory plot drew "the game's own VRAM" from the per-present <c>vram_proc</c> blob, which the
/// Overlay never produced — that line was never drawn on any session (<c>20_OPEN_QUESTIONS</c> §H10).
/// </remarks>
public partial class SensorsChart : UserControl
{
    /// <summary>The temperatures-and-load plot's series, each with the palette key it is drawn in and whether it is on the watt axis.</summary>
    internal static readonly IReadOnlyList<(string Series, string PaletteKey, bool Watts)> TempsPlot =
    [
        ("gpu_temp", "Chart_Sensor", false),
        ("gpu_hotspot", "Chart_Stutter", false),
        (SensorSeriesCatalog.GpuMemTemp, "Chart_SensorTertiary", false),
        ("gpu_load", "Chart_Native", false),
        ("gpu_power", "Chart_SensorSecondary", true),
        ("cpu_temp", "Chart_Percentile", false),
        ("cpu_load", "Chart_Displayed", false),
    ];

    /// <summary>The video-memory plot's series and their palette keys: this game is in the same colour on both memory plots.</summary>
    internal static readonly IReadOnlyList<(string Series, string PaletteKey)> VideoMemoryPlot =
    [
        (SensorSeriesCatalog.GameVramDedicated, "Chart_Displayed"),
        (SensorSeriesCatalog.GameVramShared, "Chart_Native"),
        (SensorSeriesCatalog.VramAdapter, "Chart_SensorSecondary"),
    ];

    /// <summary>
    /// The memory plot's series: the game's private working set — or, where this Windows does not report it, its working set,
    /// labelled as such (<see cref="SystemMemorySeries"/>) — and the whole PC's memory in use.
    /// </summary>
    internal static readonly IReadOnlyList<(string Series, string PaletteKey)> SystemMemoryPlot =
    [
        (SensorSeriesCatalog.GameRamPrivate, "Chart_Displayed"),
        (SensorSeriesCatalog.RamSystem, "Chart_Percentile"),
    ];

    /// <summary>The clocks plot's lines (beta.14): core and memory clock on the MHz axis, the fan on the right-hand RPM axis.</summary>
    internal static readonly IReadOnlyList<(string Series, string PaletteKey, bool Rpm)> ClocksPlot =
    [
        (SensorSeriesCatalog.GpuCoreClock, "Chart_Native", false),
        (SensorSeriesCatalog.GpuMemClock, "Chart_Displayed", false),
        (SensorSeriesCatalog.GpuFan, "Chart_SensorSecondary", true),
    ];

    /// <summary>The limits shaded on the clocks plot (beta.14): the ticks each one held the card back, NVIDIA only.</summary>
    internal static readonly IReadOnlyList<(string Series, string PaletteKey)> LimitSpans =
    [
        (SensorSeriesCatalog.GpuPowerLimit, "Chart_Stutter"),
        (SensorSeriesCatalog.GpuThermalLimit, "Chart_StutterPso"),
    ];

    private const double _mibPerGb = 1024.0;

    public SensorsChart()
    {
        InitializeComponent();
        ChartTheme.Attach(Temps);
        ChartTheme.Attach(Vram);
        ChartTheme.Attach(Ram);
        ChartTheme.Attach(Clocks);
    }

    /// <summary>The shaded spans the last <see cref="Show"/> drew on the clocks plot.</summary>
    public int LimitSpansDrawn { get; private set; }

    public int DrawnSeries { get; private set; }

    /// <summary>The temperatures plot, for a test's look at its axes.</summary>
    public ScottPlot.Plot TempsPlotView => Temps.Plot;

    public void Show(SessionSeries? series)
    {
        DrawnSeries = 0;
        DrawTemps(series);
        DrawMemory(Vram, series, VideoMemoryPlot);
        DrawMemory(Ram, series, SystemMemoryPlot);
        DrawClocks(series);
    }

    /// <summary>
    /// The card's clocks and fan (beta.14), and every run of ticks a limit held it back as a shaded span behind them — a span
    /// from the first such tick to the next tick that was not, so one tick reads as a second. The section collapses for a
    /// session that stored none of these series (recorded before beta.14, or a card no layer reads them on).
    /// </summary>
    private void DrawClocks(SessionSeries? series)
    {
        ScottPlot.Plot plot = Clocks.Plot;
        plot.Clear();
        ChartTheme.Apply(plot);
        plot.XLabel(TimeAxisLabel(series));
        plot.YLabel(Strings.Sensors_Axis_Mhz);
        LimitSpansDrawn = 0;
        bool any = false;
        if (series is not null)
        {
            ChartPalette p = ChartTheme.Current;
            foreach ((string name, string key) in LimitSpans)
            {
                string label = string.Equals(name, SensorSeriesCatalog.GpuPowerLimit, StringComparison.Ordinal) ? Strings.Sensors_Limit_Power : Strings.Sensors_Limit_Thermal;
                LimitSpansDrawn += Spans(plot, series, name, label, p.ByKey(key));
            }

            foreach ((string name, string key, bool rpm) in ClocksPlot)
            {
                bool drawn = Line(plot, series, name, Label(name), p.ByKey(key), rpm ? plot.Axes.Right : null);
                any |= drawn;
                if (drawn && rpm)
                {
                    plot.Axes.Right.Label.Text = Strings.Sensors_Axis_Rpm;
                }
            }

            any |= LimitSpansDrawn > 0;
            plot.Axes.AutoScale();
            plot.ShowLegend(Alignment.UpperRight);
        }

        ClocksSection.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        Clocks.Refresh();
    }

    /// <summary>Shades every run of ticks whose value is 100 (held back) in <paramref name="name"/>; how many spans it drew.</summary>
    private static int Spans(ScottPlot.Plot plot, SessionSeries series, string name, string label, Color color)
    {
        SensorSeries? limit = series.Sensors.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));
        if (limit is null)
        {
            return 0;
        }

        int drawn = 0;
        foreach ((double from, double to) in LimitRuns(limit))
        {
            VerticalSpan span = plot.Add.VerticalSpan(from, to, color.WithAlpha(0.18));
            span.LineWidth = 0;
            if (drawn++ == 0)
            {
                span.LegendText = label;
            }
        }

        return drawn;
    }

    /// <summary>
    /// The runs of ticks held back, as (start, end) seconds: from a tick at 100 to the next tick, and through every tick at
    /// 100 after it — the reading holds for the second until the next. Pure, for the tests.
    /// </summary>
    internal static IReadOnlyList<(double From, double To)> LimitRuns(SensorSeries limit)
    {
        ArgumentNullException.ThrowIfNull(limit);
        var runs = new List<(double, double)>();
        double? start = null;
        for (int i = 0; i < limit.Values.Length; i++)
        {
            bool held = limit.Values[i] >= 50;
            if (held && start is null)
            {
                start = limit.TimesS[i];
            }
            else if (!held && start is double s)
            {
                runs.Add((s, limit.TimesS[i]));
                start = null;
            }
        }

        if (start is double open)
        {
            double last = limit.TimesS[^1];
            runs.Add((open, last > open ? last + (last - limit.TimesS[Math.Max(0, limit.TimesS.Length - 2)]) : open + 1));
        }

        return runs;
    }

    /// <summary>The time axis says where its zero is: the first frame when the sensors sit on the frames' axis, the session's start otherwise.</summary>
    internal static string TimeAxisLabel(SessionSeries? series) =>
        series is { SensorsAligned: true, Presents: > 0 } ? Strings.Chart_Axis_TimeFromFirstFrame : Strings.Chart_Axis_TimeFromStart;

    private void DrawTemps(SessionSeries? series)
    {
        ScottPlot.Plot plot = Temps.Plot;
        plot.Clear();
        ChartTheme.Apply(plot);
        plot.XLabel(TimeAxisLabel(series));
        plot.YLabel(Strings.Chart_Axis_Sensor);
        if (series is not null)
        {
            ChartPalette p = ChartTheme.Current;
            foreach ((string name, string key, bool watts) in TempsPlot)
            {
                if (Line(plot, series, name, Label(name), p.ByKey(key), watts ? plot.Axes.Right : null) && watts)
                {
                    plot.Axes.Right.Label.Text = Strings.Sensors_Axis_Watts;
                }
            }

            plot.Axes.AutoScale();
            plot.ShowLegend(Alignment.UpperRight);
        }

        Temps.Refresh();
    }

    /// <summary>One memory plot in GB (MiB stored, 1 GB = 1024 MiB as Task Manager counts it), every line labelled with whose memory it is.</summary>
    private void DrawMemory(WpfPlot view, SessionSeries? series, IReadOnlyList<(string Series, string PaletteKey)> lines)
    {
        ScottPlot.Plot plot = view.Plot;
        plot.Clear();
        ChartTheme.Apply(plot);
        plot.XLabel(TimeAxisLabel(series));
        plot.YLabel(Strings.Sensors_Axis_Gb);
        if (series is not null)
        {
            ChartPalette p = ChartTheme.Current;
            foreach ((string name, string key) in lines)
            {
                string shown = SystemMemorySeries(series, name);
                Line(plot, series, shown, Label(shown), p.ByKey(key), null, 1.0 / _mibPerGb);
            }

            plot.Axes.AutoScale();
            plot.ShowLegend(Alignment.UpperRight);
        }

        view.Refresh();
    }

    /// <summary>
    /// The game's private working set where it was read, else its working set — labelled as the working set, never as the
    /// private one (a Windows before the September 2023 update does not report it). Every other series is itself.
    /// </summary>
    internal static string SystemMemorySeries(SessionSeries series, string name)
    {
        ArgumentNullException.ThrowIfNull(series);
        return string.Equals(name, SensorSeriesCatalog.GameRamPrivate, StringComparison.Ordinal) && !Has(series, name) && Has(series, SensorSeriesCatalog.GameRamWorkingSet)
            ? SensorSeriesCatalog.GameRamWorkingSet
            : name;
    }

    private static bool Has(SessionSeries series, string name) =>
        series.Sensors.Any(s => string.Equals(s.Name, name, StringComparison.Ordinal) && s.TimesS.Length > 0);

    private static string Label(string series) => SensorStatsTable.Label(series);

    /// <summary>Draws one sensor series if the session has it, its values times <paramref name="scale"/>; whether it did.</summary>
    private bool Line(ScottPlot.Plot plot, SessionSeries series, string name, string label, Color color, IYAxis? axis, double scale = 1.0)
    {
        SensorSeries? sensor = series.Sensors.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));
        if (sensor is null || sensor.TimesS.Length == 0)
        {
            return false;
        }

        double[] values = scale == 1.0 ? sensor.Values : [.. sensor.Values.Select(v => v * scale)];
        (double[] xs, double[] ys) = Decimator.MinMax(sensor.TimesS, values, 1000);
        Scatter line = plot.Add.ScatterLine(xs, ys, color);
        line.LineWidth = 1;
        line.LegendText = label;
        if (axis is not null)
        {
            line.Axes.YAxis = axis;
        }

        DrawnSeries++;
        return true;
    }
}
