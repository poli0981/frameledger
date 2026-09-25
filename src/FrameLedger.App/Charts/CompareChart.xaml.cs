using System.Windows.Controls;
using ScottPlot;
using ScottPlot.Plottables;

namespace FrameLedger.App.Charts;

/// <summary>The Compare overlay: one percentile curve per hooked session; a mixed-tier comparison carries its legend as the plot's title.</summary>
public partial class CompareChart : UserControl
{
    public CompareChart()
    {
        InitializeComponent();
        ChartTheme.Attach(Plot);
    }

    public ScottPlot.Plot ScottPlot => Plot.Plot;

    public int DrawnCurves { get; private set; }

    public void Show(IReadOnlyList<CompareCurve> curves, string? tierLegend)
    {
        ArgumentNullException.ThrowIfNull(curves);
        ScottPlot.Plot plot = Plot.Plot;
        plot.Clear();
        ChartTheme.Apply(plot);
        plot.XLabel(Strings.Chart_Axis_Percentile);
        plot.YLabel(Strings.Chart_Axis_Fps);
        plot.Title(tierLegend ?? string.Empty, 12);
        ChartPalette p = ChartTheme.Current;
        Color[] palette = [p.Native, p.Displayed, p.Sensor, p.Percentile, p.Stutter];
        DrawnCurves = 0;
        foreach (CompareCurve curve in curves)
        {
            if (curve.Xs.Length == 0)
            {
                continue;
            }

            Scatter line = plot.Add.ScatterLine(curve.Xs, curve.Ys, palette[DrawnCurves % palette.Length]);
            line.LineWidth = 2;
            line.LegendText = curve.Label;
            DrawnCurves++;
        }

        if (DrawnCurves > 0)
        {
            // The percentile axis is 0–100 whatever the data: AutoScale first, then the X limits (beta.8 — the other way
            // round, AutoScale undid them).
            plot.Axes.AutoScale();
            plot.Axes.SetLimitsX(0, 100);
            plot.ShowLegend(Alignment.UpperRight);
        }

        Plot.Refresh();
    }
}
