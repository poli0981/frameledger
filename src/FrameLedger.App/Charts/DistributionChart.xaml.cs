using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ScottPlot;
using ScottPlot.Plottables;

namespace FrameLedger.App.Charts;

/// <summary>
/// FR-5.1's distribution: a histogram of instantaneous FPS over application frames, and the percentile curve —
/// FPS at each percentile of frametime under <c>03_METRICS</c>' linear method (<c>Domain.Metrics.Percentile</c>),
/// so the 1% and 0.1% lows on the stat cards are two points on this curve, marked where the session has the frames for
/// them (<see cref="PercentileCurve"/>).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
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

    /// <summary>The percentile plot, for a test's look at its axis.</summary>
    public ScottPlot.Plot PercentilePlot => Percentiles.Plot;

    public void Show(SessionSeries? series)
    {
        int outside = DrawHistogram(series);
        DrawPercentiles(series);
        Note.Text = outside > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.Chart_Note_OutsideRange_Format, outside) : string.Empty;
        Note.Visibility = outside > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Draws the histogram; the frames its range left out.</summary>
    private int DrawHistogram(SessionSeries? series)
    {
        ScottPlot.Plot plot = Histogram.Plot;
        plot.Clear();
        ChartTheme.Apply(plot);
        plot.XLabel(Strings.Chart_Axis_Fps);
        plot.YLabel(Strings.Chart_Axis_Count);
        int outside = 0;
        if (series is { AppFrameTimesMs.Length: > 1 })
        {
            FpsHistogram histogram = FpsHistogram.Of(series.AppFrameTimesMs, _bins);
            if (histogram.Centers.Length > 0)
            {
                BarPlot bars = plot.Add.Bars(histogram.Centers, histogram.Counts);
                bars.Color = ChartTheme.Current.Histogram;
                foreach (Bar bar in bars.Bars)
                {
                    bar.Size = histogram.BinWidth;
                    bar.LineWidth = 0;
                }

                outside = histogram.Outside;
                plot.Axes.AutoScale();
            }
        }

        Histogram.Refresh();
        return outside;
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
            if (xs.Length > 0)
            {
                Scatter line = plot.Add.ScatterLine(xs, ys, ChartTheme.Current.Percentile);
                line.LineWidth = 2;
                line.LegendText = Strings.Chart_Series_Percentile;
            }

            foreach ((double x, double fps, bool pointOne) in PercentileCurve.Lows(series.AppFrameTimesMs))
            {
                Scatter low = plot.Add.ScatterPoints(new[] { x }, new[] { fps }, ChartTheme.Current.Stutter);
                low.MarkerSize = 7;
                low.LegendText = pointOne ? Strings.Summary_Stat_P01Low : Strings.Summary_Stat_P1Low;
            }

            // The axis is 0–100 whatever the curve covers: AutoScale first, then the X limits (AutoScale afterwards undid them).
            plot.Axes.AutoScale();
            plot.Axes.SetLimitsX(0, 100);
            plot.ShowLegend(Alignment.LowerLeft);
        }

        Percentiles.Refresh();
    }
}
