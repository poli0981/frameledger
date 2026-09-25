using FrameLedger.Domain.Metrics;

namespace FrameLedger.App.Charts;

/// <summary>
/// The distribution tab's histogram of instantaneous FPS over application frames (FR-5.1), binned over the 0.5th to the
/// 99.5th percentile and drawn at each bin's CENTRE (beta.8). Binned over min to max, one frame of a fraction of a
/// millisecond — a present that followed another by nothing — stretched the axis to thousands of FPS and squeezed every
/// real frame into two bars; and a bar is drawn centred on its position, where ScottPlot's bins are lower edges, so every
/// bar sat half a bin to the left of what it counted.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "a data carrier for ScottPlot, which draws double[]")]
public sealed record FpsHistogram(double[] Centers, double[] Counts, double BinWidth, int Outside)
{
    /// <summary>The share cut from each end before binning.</summary>
    public const double TailFraction = 0.005;

    public static FpsHistogram Of(IReadOnlyList<double> appFrameTimesMs, int bins = 60)
    {
        ArgumentNullException.ThrowIfNull(appFrameTimesMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bins);
        double[] fps = [.. appFrameTimesMs.Where(static f => f > 0).Select(static f => 1000.0 / f)];
        if (fps.Length == 0)
        {
            return new FpsHistogram([], [], 0, 0);
        }

        IReadOnlyList<double> sorted = Percentile.Sorted(fps);
        double lo = Percentile.Linear(sorted, TailFraction) ?? sorted[0];
        double hi = Percentile.Linear(sorted, 1 - TailFraction) ?? sorted[^1];
        if (hi - lo < 1e-9)
        {
            // Every frame at one rate: one bar, a frame per second wide, centred on it.
            return new FpsHistogram([lo], [fps.Length], 1, 0);
        }

        double width = (hi - lo) / bins;
        var counts = new double[bins];
        int outside = 0;
        foreach (double v in fps)
        {
            if (v < lo || v > hi)
            {
                outside++;
                continue;
            }

            counts[Math.Min(bins - 1, (int)((v - lo) / width))]++;
        }

        double[] centers = [.. Enumerable.Range(0, bins).Select(b => lo + ((b + 0.5) * width))];
        return new FpsHistogram(centers, counts, width, outside);
    }
}
