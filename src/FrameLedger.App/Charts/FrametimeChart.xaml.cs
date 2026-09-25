using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ScottPlot;
using ScottPlot.Plottables;

namespace FrameLedger.App.Charts;

/// <summary>
/// The frametime timeline (FR-5.1, FR-5.3, FR-5.4, FR-5.5): the application-frame series by default, every present on
/// request, stutter markers coloured by cause where the PSO count says so, the segment ribbon as translucent spans with
/// their settings named, and the sensor overlay on the right axis. Series are min/max decimated per draw; the raw arrays
/// stay in the <see cref="SessionSeries"/>.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public partial class FrametimeChart : UserControl
{
    /// <summary>A segment narrower than this share of the axis keeps its shading and loses its label (the labels would overlap).</summary>
    private const double _labelledSegmentShare = 0.12;

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

    /// <summary>The note the last draw showed above the plot; empty when it had nothing to say.</summary>
    public string NoteText => Note.Text;

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

    /// <summary>
    /// What the lines are not, when that is not obvious (beta.8): a session whose presents were all generated is charted as
    /// presents; every present is timed at submission, which is not the display's cadence; and the overlay's sensors run ahead
    /// of the frames past every gap the time axis closes up.
    /// </summary>
    internal static string NoteFor(SessionSeries? series, bool displayed, bool sensors)
    {
        if (series is null || series.Presents == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>(2);
        if (series.NoApplicationFrames)
        {
            lines.Add(Strings.Chart_Note_NoApplicationFrames);
        }
        else if (displayed && series.HasGenerated)
        {
            lines.Add(Strings.Chart_Note_PresentsTiming);
        }

        int gaps = series.Gap.Skip(1).Count(static g => g);
        if (sensors && series.SensorsAligned && gaps > 0)
        {
            lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.Chart_Note_GapsClosed_Format, gaps));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void Redraw()
    {
        ScottPlot.Plot plot = Plot.Plot;
        plot.Clear();
        DrawnPoints = 0;
        ChartTheme.Apply(plot);
        plot.XLabel(Strings.Chart_Axis_Time);
        plot.YLabel(Strings.Chart_Axis_Frametime);
        Note.Text = NoteFor(_series, _displayed, _sensors);
        Note.Visibility = Note.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_series is null || _series.Presents == 0)
        {
            Plot.Refresh();
            return;
        }

        ChartPalette p = ChartTheme.Current;
        bool generating = _series.HasGenerated && !_series.NoApplicationFrames;
        DrawSegments(plot, p);
        if (_displayed && generating)
        {
            DrawPresents(plot, p);
        }

        DrawSeries(plot, _series.AppTimesS, _series.AppFrameTimesMs, p.Native, generating ? Strings.Chart_Series_AppFrames : Strings.Chart_Series_Presents);
        DrawStutter(plot, p);
        if (_sensors && _series.SensorsAligned)
        {
            DrawSensors(plot, p);
        }

        plot.Axes.AutoScale();
        LabelSegments(plot, p);
        plot.ShowLegend(Alignment.UpperRight);
        Plot.Refresh();
    }

    private void DrawSeries(ScottPlot.Plot plot, IReadOnlyList<double> xs, IReadOnlyList<double> ys, Color color, string label)
    {
        if (xs.Count == 0)
        {
            return;
        }

        (double[] dx, double[] dy) = Decimator.MinMax(xs, ys);
        DrawnPoints += dx.Length;
        SignalXY line = plot.Add.SignalXY(dx, dy, color);
        line.LineWidth = 1;
        line.LegendText = label;
    }

    /// <summary>Every present but those after a gap, whose stored interval is 0 — not a frame, and it was drawn as a 0 ms one.</summary>
    private void DrawPresents(ScottPlot.Plot plot, ChartPalette p)
    {
        var xs = new List<double>(_series!.Presents);
        var ys = new List<double>(_series.Presents);
        for (int i = 0; i < _series.Presents; i++)
        {
            if (!_series.Gap[i])
            {
                xs.Add(_series.TimesS[i]);
                ys.Add(_series.FrameTimesMs[i]);
            }
        }

        DrawSeries(plot, xs, ys, p.Displayed, Strings.Chart_Series_Displayed);
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

    /// <summary>
    /// Each segment's settings at its top-left corner (beta.8: the label was computed for every segment and drawn for none),
    /// where the segment is wide enough to hold it. Placed after the axes are scaled, at their top.
    /// </summary>
    private void LabelSegments(ScottPlot.Plot plot, ChartPalette p)
    {
        if (_series is null || _series.Segments.Count < 2)
        {
            return;
        }

        AxisLimits limits = plot.Axes.GetLimits();
        double width = limits.Right - limits.Left;
        foreach (SegmentSpan segment in _series.Segments.Where(s => s.Label.Length > 0 && s.EndS - s.StartS >= width * _labelledSegmentShare))
        {
            Text label = plot.Add.Text(segment.Label, segment.StartS, limits.Top);
            label.LabelFontSize = 11;
            label.LabelFontColor = p.Axis;
            label.LabelAlignment = Alignment.UpperLeft;
            label.LabelOffsetX = 4;
            label.LabelOffsetY = 4;
        }
    }

    /// <summary>
    /// GPU temperature and load on the right axis, over the frames' span only: the sensors sit on the frames' axis when the
    /// blob said where the first present is (schema 0013), and ticks before it are the launch and the loading screen.
    /// </summary>
    private void DrawSensors(ScottPlot.Plot plot, ChartPalette p)
    {
        if (_series is null)
        {
            return;
        }

        IYAxis right = plot.Axes.Right;
        right.Label.Text = Strings.Chart_Axis_Sensor;
        int n = 0;
        double end = _series.DurationS;
        foreach (SensorSeries sensor in _series.Sensors.Where(static s => s.Name is "gpu_temp" or "gpu_load"))
        {
            int[] inside = [.. Enumerable.Range(0, sensor.TimesS.Length).Where(i => sensor.TimesS[i] >= 0 && sensor.TimesS[i] <= end)];
            if (inside.Length == 0)
            {
                continue;
            }

            (double[] dx, double[] dy) = Decimator.MinMax([.. inside.Select(i => sensor.TimesS[i])], [.. inside.Select(i => sensor.Values[i])], 1000);
            DrawnPoints += dx.Length;
            Scatter line = plot.Add.ScatterLine(dx, dy, n++ == 0 ? p.Sensor : p.SensorSecondary);
            line.LineWidth = 1;
            line.LegendText = string.Equals(sensor.Name, "gpu_temp", StringComparison.Ordinal) ? Strings.Chart_Series_GpuTemp : Strings.Chart_Series_GpuLoad;
            line.Axes.YAxis = right;
        }
    }
}
