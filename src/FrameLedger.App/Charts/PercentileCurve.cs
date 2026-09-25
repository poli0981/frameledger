using FrameLedger.Domain.Metrics;

namespace FrameLedger.App.Charts;

/// <summary>
/// FPS at each percentile of frametime, under <c>03_METRICS</c>' linear method — the distribution tab's curve and Compare's
/// overlay — drawn only where the session has the frames to say it (beta.8).
/// </summary>
/// <remarks>
/// <b>A percentile is drawn when at least <see cref="FramesBeyond"/> frames lie beyond it on each side</b> — FR-4.8's rule
/// generalised: the 1% low (the 99th percentile) needs 1,000 frames and the 0.1% low (the 99.9th) 10,000, which is exactly
/// ten frames beyond each. The curve drew 0 to 100 whatever the count: its 100th point was the single worst frame, its 0th
/// the single shortest interval — thousands of FPS that set the axis for every other point — and its 99th was a 1% low the
/// stat card beside it refused to publish.
/// </remarks>
public static class PercentileCurve
{
    /// <summary>Frames that must lie beyond a percentile, on each side, for it to be drawn.</summary>
    public const int FramesBeyond = 10;

    public static (double[] Xs, double[] Ys) Of(IReadOnlyList<double> appFrameTimesMs)
    {
        ArgumentNullException.ThrowIfNull(appFrameTimesMs);
        if (appFrameTimesMs.Count < 2)
        {
            return ([], []);
        }

        IReadOnlyList<double> sorted = Percentile.Sorted(appFrameTimesMs);
        var xs = new List<double>(103);
        var ys = new List<double>(103);
        foreach (double p in Percents())
        {
            if (Supported(sorted.Count, p) && Fps(sorted, p) is double fps)
            {
                xs.Add(p);
                ys.Add(fps);
            }
        }

        return ([.. xs], [.. ys]);
    }

    /// <summary>The 1% and 0.1% lows as points on the curve (x 99 and 99.9), each only where the session has the frames for it.</summary>
    public static IReadOnlyList<(double X, double Fps, bool PointOne)> Lows(IReadOnlyList<double> appFrameTimesMs)
    {
        ArgumentNullException.ThrowIfNull(appFrameTimesMs);
        IReadOnlyList<double> sorted = Percentile.Sorted(appFrameTimesMs);
        var lows = new List<(double, double, bool)>(2);
        if (Supported(sorted.Count, 99) && Fps(sorted, 99) is double p1)
        {
            lows.Add((99, p1, false));
        }

        if (Supported(sorted.Count, 99.9) && Fps(sorted, 99.9) is double p01)
        {
            lows.Add((99.9, p01, true));
        }

        return lows;
    }

    /// <summary>At least <see cref="FramesBeyond"/> of <paramref name="frames"/> lie beyond <paramref name="percent"/> on each side.</summary>
    public static bool Supported(int frames, double percent) =>
        frames * Math.Min(percent, 100 - percent) / 100.0 >= FramesBeyond - 1e-9;

    /// <summary>0, 0.1, 1 … 99, 99.9, 100 — in order, because the curve is drawn as a line through them.</summary>
    private static IEnumerable<double> Percents()
    {
        yield return 0;
        yield return 0.1;
        for (int i = 1; i <= 99; i++)
        {
            yield return i;
        }

        yield return 99.9;
        yield return 100;
    }

    private static double? Fps(IReadOnlyList<double> sorted, double percent) =>
        Percentile.Linear(sorted, percent / 100.0) is > 0 and double ft ? 1000.0 / ft : null;
}
