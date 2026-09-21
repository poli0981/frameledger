using System.Windows.Controls;
using ScottPlot;
using ScottPlot.Plottables;

namespace FrameLedger.App.Charts;

/// <summary>
/// The Sensors tab: GPU core / hotspot temperature and load / power on one plot (°C and % left, W right),
/// and VRAM on another — the game's own usage (the per-present <c>vram_proc</c> blob, a held 1 Hz sample)
/// and the adapter-wide figure (the <c>vram_adapter</c> sensor) as two differently-labelled series.
/// </summary>
public partial class SensorsChart : UserControl
{
    public SensorsChart()
    {
        InitializeComponent();
        ChartTheme.Attach(Temps);
        ChartTheme.Attach(Vram);
    }

    public int DrawnSeries { get; private set; }

    public void Show(SessionSeries? series)
    {
        DrawnSeries = 0;
        DrawTemps(series);
        DrawVram(series);
    }

    private void DrawTemps(SessionSeries? series)
    {
        ScottPlot.Plot plot = Temps.Plot;
        plot.Clear();
        ChartTheme.Apply(plot);
        plot.XLabel(Strings.Chart_Axis_Time);
        plot.YLabel(Strings.Chart_Axis_Sensor);
        if (series is not null)
        {
            ChartPalette p = ChartTheme.Current;
            Line(plot, series, "gpu_temp", Strings.Sensors_Series_GpuTemp, p.Sensor, null);
            Line(plot, series, "gpu_hotspot", Strings.Sensors_Series_GpuHotspot, p.Stutter, null);
            Line(plot, series, "gpu_load", Strings.Sensors_Series_GpuLoad, p.Native, null);
            Line(plot, series, "gpu_power", Strings.Sensors_Series_GpuPower, p.SensorSecondary, plot.Axes.Right);
            Line(plot, series, "cpu_temp", Strings.Sensors_Series_CpuTemp, p.Percentile, null);
            Line(plot, series, "cpu_load", Strings.Sensors_Series_CpuLoad, p.Displayed, null);
            plot.Axes.AutoScale();
            plot.ShowLegend(Alignment.UpperRight);
        }

        Temps.Refresh();
    }

    private void DrawVram(SessionSeries? series)
    {
        ScottPlot.Plot plot = Vram.Plot;
        plot.Clear();
        ChartTheme.Apply(plot);
        plot.XLabel(Strings.Chart_Axis_Time);
        plot.YLabel(Strings.Sensors_Axis_Mb);
        if (series is not null)
        {
            ChartPalette p = ChartTheme.Current;
            if (series.VramProcMb is { Length: > 0 } proc)
            {
                int n = Math.Min(proc.Length, series.TimesS.Length);
                (double[] xs, double[] ys) = Decimator.MinMax(series.TimesS[..n], [.. proc.Take(n).Select(static v => (double)v)], 1000);
                Scatter line = plot.Add.ScatterLine(xs, ys, p.Displayed);
                line.LineWidth = 1;
                line.LegendText = Strings.Sensors_Series_VramProcess;
                DrawnSeries++;
            }

            Line(plot, series, "vram_adapter", Strings.Sensors_Series_VramAdapter, p.SensorSecondary, null);
            Line(plot, series, "ram_mb", Strings.Sensors_Series_Ram, p.Percentile, null);
            plot.Axes.AutoScale();
            plot.ShowLegend(Alignment.UpperRight);
        }

        Vram.Refresh();
    }

    private void Line(ScottPlot.Plot plot, SessionSeries series, string name, string label, Color color, IYAxis? axis)
    {
        SensorSeries? sensor = series.Sensors.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));
        if (sensor is null || sensor.TimesS.Length == 0)
        {
            return;
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
    }
}
