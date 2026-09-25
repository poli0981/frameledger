using System.Windows.Controls;
using ScottPlot;
using ScottPlot.Plottables;

namespace FrameLedger.App.Charts;

/// <summary>
/// The Sensors tab: GPU core / hotspot temperature, load, CPU temperature and load on one plot (°C and % left) with the GPU's
/// power on a watt axis at the right, and memory on another — the game's own VRAM (the per-present <c>vram_proc</c> blob, a
/// held 1 Hz sample), the adapter-wide VRAM (the <c>vram_adapter</c> sensor) and the system's RAM as differently-labelled
/// series. Since beta.8 it draws a session that was not hooked too: its sensors are all it has.
/// </summary>
public partial class SensorsChart : UserControl
{
    /// <summary>The temperatures-and-load plot's series, each with the palette key it is drawn in and whether it is on the watt axis.</summary>
    internal static readonly IReadOnlyList<(string Series, string PaletteKey, bool Watts)> TempsPlot =
    [
        ("gpu_temp", "Chart_Sensor", false),
        ("gpu_hotspot", "Chart_Stutter", false),
        ("gpu_load", "Chart_Native", false),
        ("gpu_power", "Chart_SensorSecondary", true),
        ("cpu_temp", "Chart_Percentile", false),
        ("cpu_load", "Chart_Displayed", false),
    ];

    /// <summary>The memory plot's series and their palette keys; <c>vram_proc</c> is the per-present blob, the others sensors.</summary>
    internal static readonly IReadOnlyList<(string Series, string PaletteKey)> MemoryPlot =
    [
        ("vram_proc", "Chart_Displayed"),
        ("vram_adapter", "Chart_SensorSecondary"),
        ("ram_mb", "Chart_Percentile"),
    ];

    public SensorsChart()
    {
        InitializeComponent();
        ChartTheme.Attach(Temps);
        ChartTheme.Attach(Vram);
    }

    public int DrawnSeries { get; private set; }

    /// <summary>The temperatures plot, for a test's look at its axes.</summary>
    public ScottPlot.Plot TempsPlotView => Temps.Plot;

    public void Show(SessionSeries? series)
    {
        DrawnSeries = 0;
        DrawTemps(series);
        DrawMemory(series);
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

    private void DrawMemory(SessionSeries? series)
    {
        ScottPlot.Plot plot = Vram.Plot;
        plot.Clear();
        ChartTheme.Apply(plot);
        plot.XLabel(TimeAxisLabel(series));
        plot.YLabel(Strings.Sensors_Axis_Mb);
        if (series is not null)
        {
            ChartPalette p = ChartTheme.Current;
            DrawProcessVram(plot, series, p.ByKey(MemoryPlot[0].PaletteKey));
            Line(plot, series, "vram_adapter", Strings.Sensors_Series_VramAdapter, p.ByKey(MemoryPlot[1].PaletteKey), null);
            Line(plot, series, "ram_mb", Strings.Sensors_Series_Ram, p.ByKey(MemoryPlot[2].PaletteKey), null);
            plot.Axes.AutoScale();
            plot.ShowLegend(Alignment.UpperRight);
        }

        Vram.Refresh();
    }

    /// <summary>
    /// The game's own VRAM, per present on the frames' axis — a 0 is a present the sample did not reach (the record's zero,
    /// SessionFinalizer.Series), never a reading, and is left out (beta.8).
    /// </summary>
    private void DrawProcessVram(ScottPlot.Plot plot, SessionSeries series, Color color)
    {
        if (series.VramProcMb is not { Length: > 0 } proc)
        {
            return;
        }

        int n = Math.Min(proc.Length, series.TimesS.Length);
        int[] read = [.. Enumerable.Range(0, n).Where(i => proc[i] > 0)];
        if (read.Length == 0)
        {
            return;
        }

        (double[] xs, double[] ys) = Decimator.MinMax([.. read.Select(i => series.TimesS[i])], [.. read.Select(i => (double)proc[i])], 1000);
        Scatter line = plot.Add.ScatterLine(xs, ys, color);
        line.LineWidth = 1;
        line.LegendText = Strings.Sensors_Series_VramProcess;
        DrawnSeries++;
    }

    private static string Label(string series) => series switch
    {
        "gpu_temp" => Strings.Sensors_Series_GpuTemp,
        "gpu_hotspot" => Strings.Sensors_Series_GpuHotspot,
        "gpu_load" => Strings.Sensors_Series_GpuLoad,
        "gpu_power" => Strings.Sensors_Series_GpuPower,
        "cpu_temp" => Strings.Sensors_Series_CpuTemp,
        "cpu_load" => Strings.Sensors_Series_CpuLoad,
        _ => series,
    };

    /// <summary>Draws one sensor series if the session has it; whether it did.</summary>
    private bool Line(ScottPlot.Plot plot, SessionSeries series, string name, string label, Color color, IYAxis? axis)
    {
        SensorSeries? sensor = series.Sensors.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));
        if (sensor is null || sensor.TimesS.Length == 0)
        {
            return false;
        }

        (double[] xs, double[] ys) = Decimator.MinMax(sensor.TimesS, sensor.Values, 1000);
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
