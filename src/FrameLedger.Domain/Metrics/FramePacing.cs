// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>
/// <c>03_METRICS</c> §Pacing (beta.14, D49): how much of the measured time ran below 30 FPS, below 60 FPS and below the
/// monitor's refresh rate, and the mean change from one frame time to the next.
/// </summary>
/// <remarks>
/// <para>
/// <b>Time, not frames.</b> "Below 60" is the share of the measured time spent in frames that count as below 60:
/// <c>Σ ft (counted) / Σ ft</c>. A count of slow frames would weigh a long hitch as little as a short one; time weighs them
/// as a player sits through them.
/// </para>
/// <para>
/// <b>Which frames count, and why not simply "longer than 1000 / X ms".</b> The present-to-present interval of a game held
/// at X jitters around 1000 / X by a few tenths of a millisecond, half of it on the long side: on the owner's ledger a
/// GIRLS' FRONTLINE 2 session capped at 60 (median 16.63 ms, 296,380 frames) read <b>50.0 %</b> of its time "below 60" under
/// that rule. A frame counts when it is itself more than <see cref="FrameMargin"/> longer than 1000 / X — a long frame,
/// never jitter — or when the median of the <see cref="Window"/> frames around it (<see cref="RollingMedian"/>, the stutter
/// rule's edges) is more than <see cref="RateMargin"/> longer — a rate that stays below X. The same session reads 9.6 %
/// (its time below 30 alone is 4.7 %); with a 0.4 ms jitter a steady 58 FPS reads 93 %, a steady 60.2 reads 0.7 %, and a
/// 100 ms hitch counts its own 100 ms in full and spreads to no neighbour (the median of nine ignores one frame). What the
/// margins cost: a rate less than 2 % under X (58.8 to 60 FPS for 60) reads partly as at X — a steady 59 reads 40 %.
/// </para>
/// <para>
/// <b>Frame to frame</b> is the mean of <c>|ft[k] − ft[k−1]|</c> over neighbouring kept intervals — a pair with an
/// interval left out between them (<see cref="FrameTimeSeries.FollowsBreak"/>) is not one. Low when pacing is even whatever
/// the rate: a steady 30 FPS reads lower than a 60 FPS alternating 10 and 23 ms.
/// </para>
/// <para>
/// <b>Which series.</b> 30, 60 and frame to frame are over the series the frame statistics are over (application frames
/// where generated frames were counted, presents — labelled — otherwise). The refresh rate is the display's, so its share
/// is over the presents the display received, generated ones included, and is labelled as such.
/// </para>
/// <para><b>Sufficiency:</b> the 1 % low's 1,000 frames (<see cref="Percentile.P1SufficientFrames"/>); below it, null.</para>
/// </remarks>
public sealed record FramePacing
{
    /// <summary>The frames whose median says the rate: nine, centered.</summary>
    public const int Window = 9;

    /// <summary>A median this much longer than 1000 / X is a rate below X.</summary>
    public const double RateMargin = 0.02;

    /// <summary>A frame this much longer than 1000 / X is below X on its own.</summary>
    public const double FrameMargin = 0.10;

    /// <summary>Every value null: too few frames, or nothing measured.</summary>
    public static FramePacing Empty { get; } = new()
    {
        TimeBelow30Pct = null,
        TimeBelow60Pct = null,
        TimeBelowRefreshPct = null,
        FrameToFrameMeanMs = null,
    };

    /// <summary>Percent of the measured time below 30 FPS.</summary>
    public required double? TimeBelow30Pct { get; init; }

    /// <summary>Percent of the measured time below 60 FPS.</summary>
    public required double? TimeBelow60Pct { get; init; }

    /// <summary>Percent of the presented time below the monitor's refresh rate; null without a rate.</summary>
    public required double? TimeBelowRefreshPct { get; init; }

    /// <summary>Mean <c>|ft[k] − ft[k−1]|</c> over neighbouring intervals, ms.</summary>
    public required double? FrameToFrameMeanMs { get; init; }

    /// <summary>
    /// The four values: 30, 60 and frame to frame over <paramref name="frames"/>, the refresh share over
    /// <paramref name="presents"/> at <paramref name="refreshHz"/>.
    /// </summary>
    public static FramePacing From(FrameTimeSeries? frames, FrameTimeSeries? presents, double? refreshHz)
    {
        Timeline? f = Timeline.Of(frames);
        Timeline? p = refreshHz is > 0 ? Timeline.Of(presents) : null;
        if (f is null && p is null)
        {
            return Empty;
        }

        return new FramePacing
        {
            TimeBelow30Pct = f?.TimeBelowPct(30),
            TimeBelow60Pct = f?.TimeBelowPct(60),
            TimeBelowRefreshPct = p?.TimeBelowPct(refreshHz!.Value),
            FrameToFrameMeanMs = f?.FrameToFrameMeanMs(),
        };
    }

    /// <summary>One series with its rolling medians, computed once for every threshold.</summary>
    private sealed class Timeline
    {
        private readonly IReadOnlyList<double> _ft;
        private readonly IReadOnlyList<bool> _followsBreak;
        private readonly IReadOnlyList<double> _median;
        private readonly double _total;

        private Timeline(FrameTimeSeries series, IReadOnlyList<double> median, double total)
        {
            _ft = series.FrameTimesMs;
            _followsBreak = series.FollowsBreak;
            _median = median;
            _total = total;
        }

        public static Timeline? Of(FrameTimeSeries? series)
        {
            if (series is null || series.Count < Percentile.P1SufficientFrames)
            {
                return null;
            }

            double total = series.FrameTimesMs.Sum();
            IReadOnlyList<double>? median = RollingMedian.Of(series.FrameTimesMs, Window);
            return total > 0 && median is not null ? new Timeline(series, median, total) : null;
        }

        public double TimeBelowPct(double fps)
        {
            double limit = 1000.0 / fps;
            double slow = 0;
            for (int k = 0; k < _ft.Count; k++)
            {
                if (_ft[k] > limit * (1 + FrameMargin) || _median[k] > limit * (1 + RateMargin))
                {
                    slow += _ft[k];
                }
            }

            return slow * 100.0 / _total;
        }

        public double? FrameToFrameMeanMs()
        {
            double sum = 0;
            int pairs = 0;
            for (int k = 1; k < _ft.Count; k++)
            {
                if (!_followsBreak[k])
                {
                    sum += Math.Abs(_ft[k] - _ft[k - 1]);
                    pairs++;
                }
            }

            return pairs > 0 ? sum / pairs : null;
        }
    }
}
