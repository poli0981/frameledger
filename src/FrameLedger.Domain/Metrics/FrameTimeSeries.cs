namespace FrameLedger.Domain.Metrics;

/// <summary>
/// <c>03_METRICS</c> §Frame times: <c>ft[i] = (qpc[i] − qpc[i−1]) / qpcFreq × 1000</c> ms, measured at present
/// entry, with the intervals that are not frame times left out rather than counted as long frames.
/// </summary>
/// <remarks>
/// <para>
/// <b>Data gaps are EXCLUDED, not merged.</b> Where the drain recorded a gap (a torn or overwritten slot,
/// <c>07_IPC</c> §Protocol rules), the interval spanning it is dropped from the series entirely. Counting it
/// as one long frame would fabricate a stutter — the exact artifact these metrics exist to detect honestly.
/// A golden test asserts the gap does not appear as a stutter.
/// </para>
/// <para>
/// Non-positive intervals are dropped for the reason <see cref="RecordWindow.SecondsOf{T}"/> gives: a drain
/// that laps the ring can return an older record after a newer one, and a negative delta is not a frame
/// time in either direction.
/// </para>
/// <para>
/// <b>The average's time base leaves the gaps out too (beta.8).</b> A pause (FR-3.9) is a gap since then — the first
/// record after the resume carries one — and an average over first-to-last present would have spread the frames of
/// the measured time over the paused minutes as well. <see cref="MeasuredSeconds"/> is the time the kept intervals
/// span; with no gap it is <see cref="DurationSeconds"/>.
/// </para>
/// </remarks>
public sealed record FrameTimeSeries
{
    /// <summary>The kept intervals, in ms, in time order.</summary>
    public required IReadOnlyList<double> FrameTimesMs { get; init; }

    /// <summary>For each kept interval, the index of the sample that ENDS it — the frame the interval belongs to.</summary>
    public required IReadOnlyList<int> EndingSample { get; init; }

    /// <summary>Intervals excluded because a gap sat inside them.</summary>
    public required int ExcludedForGaps { get; init; }

    /// <summary>Intervals excluded because the clock did not advance across them.</summary>
    public required int ExcludedNonPositive { get; init; }

    /// <summary>First present to last present, in seconds: the wall clock the series spans, gaps included.</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>
    /// <c>D</c>, the time base of every average: the seconds the kept intervals span — <see cref="DurationSeconds"/> less
    /// every interval a gap sat in (a pause, lost records).
    /// </summary>
    public required double MeasuredSeconds { get; init; }

    /// <summary>Presents in the stream.</summary>
    public required int Presents { get; init; }

    public int Count => FrameTimesMs.Count;

    /// <summary>
    /// The time-based average: frames over <c>D</c>, NOT the mean of instantaneous FPS values, which weights fast
    /// frames more than slow ones — the kept intervals over the seconds they span, which is <c>(presents − 1) / D</c>
    /// wherever nothing was left out. Null without a duration to divide by.
    /// </summary>
    public double? AverageFps => MeasuredSeconds > 0 && Count > 0 ? Count / MeasuredSeconds : null;

    /// <summary>
    /// Builds the series over one stream. <paramref name="gapBefore"/> holds the indices of samples that follow
    /// a gap: the interval ending at each of them is excluded.
    /// </summary>
    public static FrameTimeSeries From(IReadOnlyList<FrameSample> stream, IReadOnlySet<int>? gapBefore, long qpcFrequency)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(qpcFrequency);

        var times = new List<double>(Math.Max(0, stream.Count - 1));
        var ending = new List<int>(times.Capacity);
        int gaps = 0;
        int nonPositive = 0;
        for (int i = 1; i < stream.Count; i++)
        {
            if (gapBefore?.Contains(i) == true)
            {
                gaps++;
                continue;
            }

            long delta = (long)stream[i].Qpc - (long)stream[i - 1].Qpc;
            if (delta <= 0)
            {
                nonPositive++;
                continue;
            }

            times.Add(delta * 1000.0 / qpcFrequency);
            ending.Add(i);
        }

        double duration = stream.Count > 1
            ? Math.Max(0, ((long)stream[^1].Qpc - (long)stream[0].Qpc) / (double)qpcFrequency)
            : 0;

        return new FrameTimeSeries
        {
            FrameTimesMs = times,
            EndingSample = ending,
            ExcludedForGaps = gaps,
            ExcludedNonPositive = nonPositive,
            DurationSeconds = duration,
            MeasuredSeconds = times.Sum() / 1000.0,
            Presents = stream.Count,
        };
    }

    /// <summary>
    /// The APPLICATION-frame series over one stream on which frame generation was counted — <c>ft_app</c>, every
    /// statistic <c>03_METRICS</c> §Core definitions takes over it: the interval between two consecutive presents that
    /// carried an application frame (<see cref="MeasuredFields.FgCounts"/> claimed, <see cref="FrameSample.FgEvaluations"/>
    /// above zero), ending at the second. The generated presents between them add no interval of their own.
    /// <see cref="Presents"/> counts the application-frame presents and <see cref="DurationSeconds"/> spans the first to the
    /// last of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A chain is broken — the interval left out and counted in <see cref="ExcludedForGaps"/> — by a gap anywhere
    /// inside it, and by a sample that does not claim the count</b>, of which nobody said whether it was generated. A clock
    /// that did not advance is <see cref="ExcludedNonPositive"/>, as in <see cref="From"/>.
    /// </para>
    /// <para>
    /// <b>Why not the interval to the previous present (beta.8, 2026-09-25).</b> The owner's Onimusha demo capture (DLSS
    /// Frame Generation ×3) submits each generated frame about half a millisecond after its application frame, so its
    /// present-to-present intervals run 0.5 / 0.5 / 15.7 ms: the median over presents read 2,073 FPS against a native 60,
    /// and the chart's "native" series — the interval from each application frame to the present before it — was a
    /// generated frame's half millisecond. Application frame to application frame reads a 60.3 FPS median.
    /// </para>
    /// <para>
    /// A present that drained two tokens is one timestamp and one application frame here: the drain attributes a token to
    /// the next present, which is accurate to one present (<c>03_METRICS</c> §Frame Generation) and was measured regular —
    /// application, generated, generated, repeating — on that title.
    /// </para>
    /// </remarks>
    public static FrameTimeSeries ApplicationFrames(IReadOnlyList<FrameSample> stream, IReadOnlySet<int>? gapBefore, long qpcFrequency)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(qpcFrequency);

        var times = new List<double>();
        var ending = new List<int>();
        int gaps = 0;
        int nonPositive = 0;
        int applicationFrames = 0;
        int first = -1;
        int previous = -1;
        bool broken = false;
        for (int i = 0; i < stream.Count; i++)
        {
            FrameSample s = stream[i];
            broken |= gapBefore?.Contains(i) == true || !s.Claims(MeasuredFields.FgCounts);
            if (!s.Claims(MeasuredFields.FgCounts) || s.FgEvaluations == 0)
            {
                continue;
            }

            applicationFrames++;
            if (first < 0)
            {
                first = i;
            }
            else if (broken)
            {
                gaps++;
            }
            else
            {
                long delta = (long)s.Qpc - (long)stream[previous].Qpc;
                if (delta > 0)
                {
                    times.Add(delta * 1000.0 / qpcFrequency);
                    ending.Add(i);
                }
                else
                {
                    nonPositive++;
                }
            }

            previous = i;
            broken = false;
        }

        return new FrameTimeSeries
        {
            FrameTimesMs = times,
            EndingSample = ending,
            ExcludedForGaps = gaps,
            ExcludedNonPositive = nonPositive,
            DurationSeconds = first < 0 ? 0 : Math.Max(0, ((long)stream[previous].Qpc - (long)stream[first].Qpc) / (double)qpcFrequency),
            MeasuredSeconds = times.Sum() / 1000.0,
            Presents = applicationFrames,
        };
    }
}
