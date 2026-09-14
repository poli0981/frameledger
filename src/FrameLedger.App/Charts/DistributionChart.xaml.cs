using System.Windows.Controls;
using ScottPlot;
using ScottPlot.Plottables;

namespace FrameLedger.App.Charts;

/// <summary>
/// FR-5.1's distribution: a histogram of instantaneous FPS over application frames, and the percentile curve —
/// FPS at each percentile of frametime under <c>03_METRICS</c>' linear method (<c>Domain.Metrics.Percentile</c>),
/// so the 1% and 0.1% lows on the stat cards are two points on this curve.
/// </summary>
public partial class DistributionChart : UserControl
{
    private const int _bins = 60;

    public DistributionChart()
    {
        InitializeComponent();
        ChartTheme.Attach(Histogram);
        ChartTheme.Attach(Percentiles);
    }

    public ScottPlot.Plot HistogramPlot => Histogram.Plot;

    public void Show(SessionSeries? series)
    {
        DrawHistogram(series);
        DrawPercentiles(series);
    }

    private void DrawHistogram(SessionSeries? series)
    {
        ScottPlot.Plot plot = Histogram.Plot;
        plot.Clear();
        ChartTheme.Apply(plot);
        plot.XLabel(Strings.Chart_Axis_Fps);
        plot.YLabel(Strings.Chart_Axis_Count);
        if (series is { AppFrameTimesMs.Length: > 1 })
        {
            double[] fps = [.. series.AppFrameTimesMs.Where(static f => f > 0).Select(static f => 1000.0 / f)];
            if (fps.Length > 1)
            {
                var histogram = ScottPlot.Statistics.Histogram.WithBinCount(_bins, fps);
                BarPlot bars = plot.Add.Bars(histogram.Bins, histogram.Counts.Select(static c => (double)c));
                bars.Color = ChartTheme.Current.Histogram;
                foreach (Bar bar in bars.Bars)
                {
                    bar.Size = histogram.FirstBinSize;
                    bar.LineWidth = 0;
                }

                plot.Axes.AutoScale();
            }
        }

        Histogram.Refresh();
    }

    private void DrawPercentiles(SessionSeries? series)
    {
        ScottPlot.Plot plot = Percentiles.Plot;
        plot.Clear();
        ChartTheme.Apply(plot);
        plot.XLabel(Strings.Chart_Axis_Percentile);
        plot.YLabel(Strings.Chart_Axis_Fps);
        if (series is { AppFrameTimesMs.Length: > 1 })
        {
            (double[] xs, double[] ys) = PercentileCurve.Of(series.AppFrameTimesMs);
            Scatter line = plot.Add.ScatterLine(xs, ys, ChartTheme.Current.Percentile);
            line.LineWidth = 2;
            line.LegendText = Strings.Chart_Series_Percentile;
            plot.Axes.SetLimitsX(0, 100);
            plot.Axes.AutoScale();
        }

        Percentiles.Refresh();
    }
}
