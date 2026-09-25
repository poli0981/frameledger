using System.Windows.Controls;
using ScottPlot;
using ScottPlot.Plottables;

namespace FrameLedger.App.Charts;

/// <summary>The trend line (one point per session, oldest first) with FR-6.3's hardware-change markers as labelled vertical lines.</summary>
public partial class TrendChart : UserControl
{
    public TrendChart()
    {
        InitializeComponent();
        ChartTheme.Attach(Plot);
    }

    public ScottPlot.Plot ScottPlot => Plot.Plot;

    public int DrawnPoints { get; private set; }

    /// <summary>How many of <see cref="DrawnPoints"/> were drawn hollow (a part of their session).</summary>
    public int HollowPoints { get; private set; }

    public void Show(IReadOnlyList<TrendPoint> points, IReadOnlyList<HardwareChange> changes, string metricLabel)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(changes);
        ScottPlot.Plot plot = Plot.Plot;
        plot.Clear();
        // The date axis REPLACES the bottom axis, so it goes first (beta.8): made after the theme and the label, it dropped
        // both — an unlabelled axis in the default colours, unreadable in the dark theme.
        plot.Axes.DateTimeTicksBottom();
        ChartTheme.Apply(plot);
        plot.XLabel(Strings.Trend_Axis_Date);
        plot.YLabel(metricLabel);
        DrawnPoints = points.Count;
        HollowPoints = points.Count(static t => t.SettingsChangedMidSession);
        if (points.Count > 0)
        {
            ChartPalette p = ChartTheme.Current;
            double[] xs = [.. points.Select(static t => t.At.LocalDateTime.ToOADate())];
            double[] ys = [.. points.Select(static t => t.Value)];
            Scatter line = plot.Add.Scatter(xs, ys, p.Native);
            line.LineWidth = 2;
            line.MarkerSize = 7;
            line.LegendText = metricLabel;

            // A point that describes part of its session (settings changed, or a steady state) is drawn hollow over its
            // filled one: the line still passes through it, and it cannot be read as the session's own number.
            TrendPoint[] partial = [.. points.Where(static t => t.SettingsChangedMidSession)];
            if (partial.Length > 0)
            {
                double[] hx = [.. partial.Select(static t => t.At.LocalDateTime.ToOADate())];
                double[] hy = [.. partial.Select(static t => t.Value)];
                Scatter hollow = plot.Add.ScatterPoints(hx, hy, p.Data);
                hollow.MarkerSize = 6;
                hollow.MarkerShape = MarkerShape.FilledCircle;
            }

            foreach (HardwareChange change in TrendSeriesBuilder.MergedByDay(changes))
            {
                VerticalLine marker = plot.Add.VerticalLine(change.At.LocalDateTime.ToOADate(), 1, p.StutterPso, LinePattern.Dashed);
                marker.LabelText = change.Text;
                marker.LabelFontSize = 11;
                marker.LabelOppositeAxis = true;
            }

            plot.Axes.AutoScale();
            plot.ShowLegend(Alignment.UpperLeft);
        }

        Plot.Refresh();
    }
}
