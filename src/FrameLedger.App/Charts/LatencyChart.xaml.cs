using System.Windows.Controls;
using ScottPlot;
using ScottPlot.Plottables;

namespace FrameLedger.App.Charts;

/// <summary>The Reflex PC-latency timeline (the <c>latency_us</c> blob, per present) with the row's stored avg and p95 as horizontal lines.</summary>
public partial class LatencyChart : UserControl
{
    public LatencyChart()
    {
        InitializeComponent();
        ChartTheme.Attach(Plot);
    }

    public int DrawnPoints { get; private set; }

    public void Show(SessionSeries? series, long? avgUs, long? p95Us)
    {
        ScottPlot.Plot plot = Plot.Plot;
        plot.Clear();
        ChartTheme.Apply(plot);
        plot.XLabel(Strings.Chart_Axis_Time);
        plot.YLabel(Strings.Latency_Axis);
        DrawnPoints = 0;
        if (series?.LatencyUs is { Length: > 0 } latency)
        {
            ChartPalette p = ChartTheme.Current;
            int n = Math.Min(latency.Length, series.TimesS.Length);
            var xs = new List<double>(n);
            var ys = new List<double>(n);
            for (int i = 0; i < n; i++)
            {
                if (latency[i] > 0)
                {
                    xs.Add(series.TimesS[i]);
                    ys.Add(latency[i] / 1000.0);
                }
            }

            if (xs.Count > 0)
            {
                (double[] dx, double[] dy) = Decimator.MinMax(xs, ys);
                DrawnPoints = dx.Length;
                SignalXY line = plot.Add.SignalXY(dx, dy, p.Native);
                line.LegendText = Strings.Latency_Series;
                if (avgUs is long avg)
                {
                    HorizontalLine a = plot.Add.HorizontalLine(avg / 1000.0, 1, p.Sensor, LinePattern.Dashed);
                    a.LabelText = Strings.Latency_Avg;
                }

                if (p95Us is long p95)
                {
                    HorizontalLine l = plot.Add.HorizontalLine(p95 / 1000.0, 1, p.Stutter, LinePattern.Dotted);
                    l.LabelText = Strings.Latency_P95;
                }

                plot.Axes.AutoScale();
                plot.ShowLegend(Alignment.UpperRight);
            }
        }

        Plot.Refresh();
    }
}
