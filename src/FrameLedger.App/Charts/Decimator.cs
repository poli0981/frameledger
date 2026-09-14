namespace FrameLedger.App.Charts;

/// <summary>
/// FR-5.3: min/max decimation. A series of any length is drawn as at most <c>2 × buckets</c> points — each
/// bucket contributes its minimum and its maximum, in the order they occur — so a one-frame spike survives at
/// any zoom while a 500k-frame session draws in one frame. Below the threshold the series is returned as is.
/// </summary>
public static class Decimator
{
    public const int DefaultBuckets = 2000;

    public static (double[] Xs, double[] Ys) MinMax(IReadOnlyList<double> xs, IReadOnlyList<double> ys, int buckets = DefaultBuckets)
    {
        ArgumentNullException.ThrowIfNull(xs);
        ArgumentNullException.ThrowIfNull(ys);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(buckets);
        if (xs.Count != ys.Count)
        {
            throw new ArgumentException("xs and ys differ in length", nameof(ys));
        }

        int n = xs.Count;
        if (n <= buckets * 2)
        {
            return ([.. xs], [.. ys]);
        }

        var outX = new List<double>(buckets * 2);
        var outY = new List<double>(buckets * 2);
        for (int b = 0; b < buckets; b++)
        {
            int start = (int)((long)n * b / buckets);
            int end = (int)((long)n * (b + 1) / buckets);
            if (end <= start)
            {
                continue;
            }

            int minAt = start;
            int maxAt = start;
            for (int i = start + 1; i < end; i++)
            {
                if (ys[i] < ys[minAt])
                {
                    minAt = i;
                }

                if (ys[i] > ys[maxAt])
                {
                    maxAt = i;
                }
            }

            if (minAt == maxAt)
            {
                outX.Add(xs[minAt]);
                outY.Add(ys[minAt]);
                continue;
            }

            int first = Math.Min(minAt, maxAt);
            int second = Math.Max(minAt, maxAt);
            outX.Add(xs[first]);
            outY.Add(ys[first]);
            outX.Add(xs[second]);
            outY.Add(ys[second]);
        }

        return ([.. outX], [.. outY]);
    }
}
