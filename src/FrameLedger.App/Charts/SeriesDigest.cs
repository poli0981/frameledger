// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Domain.Metrics;

namespace FrameLedger.App.Charts;

/// <summary>
/// What the charts derive from a whole session, worked out ONCE where the series is decoded — off the UI thread since
/// beta.23 (<see cref="SessionSeriesLoader.LoadAsync"/>) — instead of on every draw. Measured on 2026-10-14 over a
/// two-million-frame session: selecting it froze the window for 0.8–1.0 s, most of it the Distribution tab sorting every
/// application frame three times (its histogram, its percentile curve and the curve's lows each sorted their own copy) and
/// the Frametime chart counting gaps and copying the presents line, all on the UI thread and for tabs not even shown
/// (<c>spike-notes</c> §21). A series a test builds by hand has none: each chart then computes what it needs itself.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "a data carrier for ScottPlot, which draws double[]")]
public sealed record SeriesDigest
{
    /// <summary>The bins of <see cref="Histogram"/> — the Distribution tab's.</summary>
    public const int HistogramBins = 60;

    /// <summary>The application frames' times, ms, ascending.</summary>
    public required double[] SortedAppFrameTimesMs { get; init; }

    /// <summary><see cref="FpsHistogram.Of"/> over the application frames, at <see cref="HistogramBins"/>.</summary>
    public required FpsHistogram Histogram { get; init; }

    /// <summary><see cref="PercentileCurve.Of"/> over the application frames.</summary>
    public required (double[] Xs, double[] Ys) Percentiles { get; init; }

    /// <summary><see cref="PercentileCurve.Lows"/> over the application frames.</summary>
    public required IReadOnlyList<(double X, double Fps, bool PointOne)> Lows { get; init; }

    /// <summary>How many presents after the first follow a gap.</summary>
    public required int GapsAfterFirst { get; init; }

    /// <summary>How many presents were generated.</summary>
    public required int GeneratedCount { get; init; }

    /// <summary>Every present's time and interval but those after a gap — the Frametime chart's presents line; null when nothing was generated, where it is never drawn.</summary>
    public (double[] TimesS, double[] FrameTimesMs)? Presents { get; init; }

    /// <summary>The digest of a decoded series' arrays.</summary>
    public static SeriesDigest Of(double[] appFrameTimesMs, double[] timesS, float[] frameTimesMs, bool[] generated, bool[] gap, bool noApplicationFrames)
    {
        ArgumentNullException.ThrowIfNull(appFrameTimesMs);
        ArgumentNullException.ThrowIfNull(timesS);
        ArgumentNullException.ThrowIfNull(frameTimesMs);
        ArgumentNullException.ThrowIfNull(generated);
        ArgumentNullException.ThrowIfNull(gap);
        double[] sorted = (double[])appFrameTimesMs.Clone();
        Array.Sort(sorted);
        int gaps = 0;
        int generatedCount = 0;
        for (int i = 0; i < gap.Length; i++)
        {
            if (i > 0 && gap[i])
            {
                gaps++;
            }

            if (i < generated.Length && generated[i])
            {
                generatedCount++;
            }
        }

        return new SeriesDigest
        {
            SortedAppFrameTimesMs = sorted,
            Histogram = FpsHistogram.OfSorted(sorted, HistogramBins),
            Percentiles = PercentileCurve.OfSorted(sorted),
            Lows = PercentileCurve.LowsOfSorted(sorted),
            GapsAfterFirst = gaps,
            GeneratedCount = generatedCount,
            Presents = generatedCount > 0 && !noApplicationFrames ? PresentsWithoutGaps(timesS, frameTimesMs, gap) : null,
        };
    }

    /// <summary>Every present but those after a gap, whose stored interval is 0 — not a frame.</summary>
    internal static (double[] TimesS, double[] FrameTimesMs) PresentsWithoutGaps(double[] timesS, float[] frameTimesMs, bool[] gap)
    {
        ArgumentNullException.ThrowIfNull(timesS);
        ArgumentNullException.ThrowIfNull(frameTimesMs);
        ArgumentNullException.ThrowIfNull(gap);
        int n = Math.Min(timesS.Length, Math.Min(frameTimesMs.Length, gap.Length));
        var xs = new List<double>(n);
        var ys = new List<double>(n);
        for (int i = 0; i < n; i++)
        {
            if (!gap[i])
            {
                xs.Add(timesS[i]);
                ys.Add(frameTimesMs[i]);
            }
        }

        return ([.. xs], [.. ys]);
    }

    /// <summary>The ascending application frame times of <paramref name="series"/>: its digest's, or sorted now for a series without one.</summary>
    internal static IReadOnlyList<double> SortedOf(SessionSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        return series.Digest?.SortedAppFrameTimesMs ?? Percentile.Sorted(series.AppFrameTimesMs);
    }
}
