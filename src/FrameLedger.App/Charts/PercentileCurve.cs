using FrameLedger.Domain.Metrics;

namespace FrameLedger.App.Charts;

/// <summary>FPS at each percentile (0…100) of frametime, under <c>03_METRICS</c>' linear method — the distribution tab's curve and Compare's overlay.</summary>
public static class PercentileCurve
{
    public static (double[] Xs, double[] Ys) Of(IReadOnlyList<double> appFrameTimesMs)
    {
        ArgumentNullException.ThrowIfNull(appFrameTimesMs);
        if (appFrameTimesMs.Count < 2)
        {
            return ([], []);
        }

        IReadOnlyList<double> sorted = Percentile.Sorted(appFrameTimesMs);
        var xs = new double[101];
        var ys = new double[101];
        for (int i = 0; i <= 100; i++)
        {
            xs[i] = i;
            double? ft = Percentile.Linear(sorted, i / 100.0);
            ys[i] = ft is > 0 ? 1000.0 / ft.Value : 0;
        }

        return (xs, ys);
    }
}
