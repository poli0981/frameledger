// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Metrics;
using static FrameLedger.Domain.Tests.Metrics.SampleFixtures;

namespace FrameLedger.Domain.Tests.Metrics;

/// <summary>
/// beta.14 (D49): time below 30 / 60 / the refresh rate, and frame to frame. The rule's margins are the point: a game held at
/// 60 jitters around 16.67 ms, and "longer than 16.67 ms" read half its time as below 60 (the owner's GIRLS' FRONTLINE 2
/// session: 50.0 %).
/// </summary>
public sealed class FramePacingTests
{
    // A deterministic jitter around 60 FPS: mean 16.65 ms, four of eight values over 16.67.
    private static readonly double[] _jitterAt60 = [16.3, 16.9, 16.5, 17.0, 16.4, 16.8, 16.6, 16.7];

    private static FrameTimeSeries Series(IEnumerable<double> frameTimesMs, IReadOnlySet<int>? gapBefore = null) =>
        FrameTimeSeries.From(FromFrameTimes(frameTimesMs), gapBefore, Frequency);

    private static IEnumerable<double> Repeat(double[] cycle, int count) => Enumerable.Range(0, count).Select(i => cycle[i % cycle.Length]);

    [Fact]
    public void AGameHeldAtSixtyIsNotBelowSixtyForItsJitter()
    {
        FrameTimeSeries held = Series(Repeat(_jitterAt60, 2_000));

        FramePacing p = FramePacing.From(held, held, refreshHz: 60);

        held.FrameTimesMs.Where(static ft => ft > 1000.0 / 60).Sum().Should().BeGreaterThan(0.4 * held.FrameTimesMs.Sum(),
            "the rule without margins would have read nearly half the time as below 60");
        p.TimeBelow60Pct.Should().Be(0);
        p.TimeBelowRefreshPct.Should().Be(0, "and a 60 Hz display fed at 60 is not below its refresh rate either");
        p.TimeBelow30Pct.Should().Be(0);
    }

    [Fact]
    public void ASteadyRateUnderTheThresholdIsBelowItAllTheTime()
    {
        FramePacing p = FramePacing.From(Series(Enumerable.Repeat(17.5, 2_000)), null, null);

        p.TimeBelow60Pct.Should().BeApproximately(100, 1e-9, "57 FPS held: the median of every nine is 5 % over 16.67 ms");
        p.TimeBelow30Pct.Should().Be(0);
    }

    [Fact]
    public void AHitchCountsItsOwnTimeAndSpreadsToNoNeighbour()
    {
        double[] frames = [.. Enumerable.Repeat(16.0, 2_000)];
        frames[1_000] = 100;
        FrameTimeSeries s = Series(frames);

        FramePacing p = FramePacing.From(s, null, null);

        double expected = 100 * 100.0 / s.FrameTimesMs.Sum();
        p.TimeBelow30Pct.Should().BeApproximately(expected, 1e-9, "the 100 ms frame is below 30 on its own, and the median of nine ignores it");
        p.TimeBelow60Pct.Should().BeApproximately(expected, 1e-9, "its neighbours ran at 62.5 FPS and stay at it");
    }

    [Fact]
    public void FrameToFrameIsTheMeanChangeBetweenNeighbours()
    {
        FramePacing p = FramePacing.From(Series(Repeat([10, 20], 2_000)), null, null);

        p.FrameToFrameMeanMs.Should().BeApproximately(10, 1e-9);
    }

    [Fact]
    public void APairAcrossAGapIsNotAPair()
    {
        // 1,000 frames of 10 ms, a gap, 1,000 of 50 ms: every pair inside a run is equal; only the pair across the gap differs.
        double[] frames = [.. Enumerable.Repeat(10.0, 1_000), 999, .. Enumerable.Repeat(50.0, 1_000)];
        FrameTimeSeries s = Series(frames, new HashSet<int> { 1_001 });

        FramePacing p = FramePacing.From(s, null, null);

        s.FollowsBreak[1_000].Should().BeTrue();
        p.FrameToFrameMeanMs.Should().Be(0, "10 ms and 50 ms a gap apart are not neighbours");
    }

    [Fact]
    public void TheRefreshShareIsOverThePresentsTheDisplayReceived()
    {
        // ×2 frame generation on a 240 Hz display: application frames at 120 FPS, presents at 240.
        List<FrameSample> stream = FgStream(appFrames: 1_100, k: 2);
        FrameTimeSeries frames = FrameTimeSeries.ApplicationFrames(stream, null, Frequency);
        FrameTimeSeries presents = FrameTimeSeries.From(stream, null, Frequency);

        FramePacing p = FramePacing.From(frames, presents, refreshHz: 240);

        p.TimeBelowRefreshPct.Should().Be(0, "the display received a frame every refresh");
        p.TimeBelow60Pct.Should().Be(0);
        FramePacing.From(frames, frames, refreshHz: 240).TimeBelowRefreshPct.Should().BeApproximately(100, 1e-9,
            "over the application frames the same session would read as never reaching the refresh rate");
        FramePacing.From(frames, presents, refreshHz: null).TimeBelowRefreshPct.Should().BeNull("no refresh rate was read");
    }

    [Fact]
    public void UnderAThousandFramesNothingIsPublished()
    {
        FramePacing p = FramePacing.From(Series(Enumerable.Repeat(40.0, 999)), Series(Enumerable.Repeat(40.0, 999)), 60);

        p.Should().Be(FramePacing.Empty, "the 1 % low's sufficiency rule: 1,000 frames");
        FramePacing.From(null, null, 60).Should().Be(FramePacing.Empty);
    }
}
