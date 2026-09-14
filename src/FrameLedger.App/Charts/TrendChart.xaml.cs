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

    public void Show(IReadOnlyList<TrendPoint> points, IReadOnlyList<HardwareChange> changes, string metricLabel)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(changes);
        ScottPlot.Plot plot = Plot.Plot;
        plot.Clear();
        ChartTheme.Apply(plot);
        plot.XLabel(Strings.Trend_Axis_Date);
        plot.YLabel(metricLabel);
        plot.Axes.DateTimeTicksBottom();
        DrawnPoints = points.Count;
        if (points.Count > 0)
        {
            ChartPalette p = ChartTheme.Current;
            double[] xs = [.. points.Select(static t => t.At.LocalDateTime.ToOADate())];
            double[] ys = [.. points.Select(static t => t.Value)];
            Scatter line = plot.Add.Scatter(xs, ys, p.Native);
            line.LineWidth = 2;
            line.MarkerSize = 7;
            line.LegendText = metricLabel;
            foreach (HardwareChange change in changes)
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
