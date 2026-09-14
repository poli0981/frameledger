using System.Windows.Controls;
using ScottPlot;
using ScottPlot.Plottables;

namespace FrameLedger.App.Charts;

/// <summary>
/// The frametime timeline (FR-5.1, FR-5.3, FR-5.4, FR-5.5): the application-frame series by default, the
/// displayed (every present) series on request, stutter markers coloured by cause where the PSO count says so,
/// the segment ribbon as translucent spans, and the sensor overlay on the right axis. Series are min/max
/// decimated per draw; the raw arrays stay in the <see cref="SessionSeries"/>.
/// </summary>
public partial class FrametimeChart : UserControl
{
    private SessionSeries? _series;
    private bool _displayed;
    private bool _sensors;

    public FrametimeChart()
    {
        InitializeComponent();
        ChartTheme.Attach(Plot);
    }

    /// <summary>The plot, for a PNG export.</summary>
    public ScottPlot.Plot ScottPlot => Plot.Plot;

    /// <summary>How many points the last draw put on the axes (a test's window into the decimation).</summary>
    public int DrawnPoints { get; private set; }

    public void Show(SessionSeries? series, bool displayed, bool sensors)
    {
        _series = series;
        _displayed = displayed;
        _sensors = sensors;
        Redraw();
    }

    public void SetDisplayed(bool displayed)
    {
        _displayed = displayed;
        Redraw();
    }

    public void SetSensors(bool sensors)
    {
        _sensors = sensors;
        Redraw();
    }

    private void Redraw()
    {
        ScottPlot.Plot plot = Plot.Plot;
        plot.Clear();
        DrawnPoints = 0;
        ChartTheme.Apply(plot);
        plot.XLabel(Strings.Chart_Axis_Time);
        plot.YLabel(Strings.Chart_Axis_Frametime);
        if (_series is null || _series.Presents == 0)
        {
            Plot.Refresh();
            return;
        }

        ChartPalette p = ChartTheme.Current;
        DrawSegments(plot, p);
        if (_displayed && _series.HasGenerated)
        {
            DrawSeries(plot, _series.TimesS, [.. _series.FrameTimesMs.Select(static f => (double)f)], p.Displayed, Strings.Chart_Series_Displayed);
        }

        DrawSeries(plot, _series.AppTimesS, _series.AppFrameTimesMs, p.Native, Strings.Chart_Series_Native);
        DrawStutter(plot, p);
        if (_sensors)
        {
            DrawSensors(plot, p);
        }

        plot.Axes.AutoScale();
        plot.ShowLegend(Alignment.UpperRight);
        Plot.Refresh();
    }

    private void DrawSeries(ScottPlot.Plot plot, double[] xs, double[] ys, Color color, string label)
    {
        (double[] dx, double[] dy) = Decimator.MinMax(xs, ys);
        DrawnPoints += dx.Length;
        SignalXY line = plot.Add.SignalXY(dx, dy, color);
        line.LineWidth = 1;
        line.LegendText = label;
    }

    private void DrawStutter(ScottPlot.Plot plot, ChartPalette p)
    {
        if (_series?.Stutter is null)
        {
            return;
        }

        var xs = new List<double>();
        var ys = new List<double>();
        var psoXs = new List<double>();
        var psoYs = new List<double>();
        for (int i = 0; i < _series.Stutter.Length; i++)
        {
            if (!_series.Stutter[i])
            {
                continue;
            }

            int present = _series.AppPresentIndex[i];
            bool pso = _series.PsoCreated is { } counts && present < counts.Length && counts[present] > 0;
            (pso ? psoXs : xs).Add(_series.AppTimesS[i]);
            (pso ? psoYs : ys).Add(_series.AppFrameTimesMs[i]);
        }

        AddMarkers(plot, xs, ys, p.Stutter, Strings.Chart_Series_Stutter);
        AddMarkers(plot, psoXs, psoYs, p.StutterPso, Strings.Chart_Series_StutterPso);
    }

    private void AddMarkers(ScottPlot.Plot plot, List<double> xs, List<double> ys, Color color, string label)
    {
        if (xs.Count == 0)
        {
            return;
        }

        (double[] dx, double[] dy) = Decimator.MinMax(xs, ys, 1000);
        DrawnPoints += dx.Length;
        Scatter markers = plot.Add.Scatter(dx, dy, color);
        markers.LineWidth = 0;
        markers.MarkerSize = 5;
        markers.LegendText = label;
    }

    private void DrawSegments(ScottPlot.Plot plot, ChartPalette p)
    {
        if (_series is null || _series.Segments.Count < 2)
        {
            return;   // one segment is the whole session; a ribbon of one colour says nothing
        }

        bool shade = false;
        foreach (SegmentSpan segment in _series.Segments)
        {
            if (shade)
            {
                plot.Add.HorizontalSpan(segment.StartS, segment.EndS, p.Segment);
            }

            shade = !shade;
        }
    }

    private void DrawSensors(ScottPlot.Plot plot, ChartPalette p)
    {
        if (_series is null)
        {
            return;
        }

        IYAxis right = plot.Axes.Right;
        right.Label.Text = Strings.Chart_Axis_Sensor;
        int n = 0;
        foreach (SensorSeries sensor in _series.Sensors.Where(static s => s.Name is "gpu_temp" or "gpu_load"))
        {
            if (sensor.TimesS.Length == 0)
            {
                continue;
            }

            (double[] dx, double[] dy) = Decimator.MinMax(sensor.TimesS, sensor.Values, 1000);
            DrawnPoints += dx.Length;
            Scatter line = plot.Add.ScatterLine(dx, dy, n++ == 0 ? p.Sensor : p.SensorSecondary);
            line.LineWidth = 1;
            line.LegendText = string.Equals(sensor.Name, "gpu_temp", StringComparison.Ordinal) ? Strings.Chart_Series_GpuTemp : Strings.Chart_Series_GpuLoad;
            line.Axes.YAxis = right;
        }
    }
}
