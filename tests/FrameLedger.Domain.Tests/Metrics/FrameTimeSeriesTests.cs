using FluentAssertions;
using FrameLedger.Domain.Metrics;
using static FrameLedger.Domain.Tests.Metrics.SampleFixtures;

namespace FrameLedger.Domain.Tests.Metrics;

/// <summary>Frame times from QPC, with the intervals that are not frame times left out rather than invented.</summary>
public sealed class FrameTimeSeriesTests
{
    [Fact]
    public void FrameTimesAreTheQpcDeltasInMilliseconds()
    {
        List<FrameSample> stream = FromFrameTimes([10, 20, 5]);

        FrameTimeSeries s = FrameTimeSeries.From(stream, null, Frequency);

        s.FrameTimesMs.Should().Equal(10, 20, 5);
        s.EndingSample.Should().Equal(1, 2, 3);
        s.Count.Should().Be(3);
        s.Presents.Should().Be(4);
        s.DurationSeconds.Should().BeApproximately(0.035, 1e-9);
        s.ExcludedForGaps.Should().Be(0);
        s.ExcludedNonPositive.Should().Be(0);
    }

    [Fact]
    public void TheAverageIsTimeBasedAndNotTheMeanOfInstantaneousFps()
    {
        // 03_METRICS: "Time-based, NOT the mean of instantaneous FPS values". One 100 ms frame and nine
        // 10 ms frames: mean-of-FPS says (9×100 + 10) / 10 = 91; presents/D says 10 / 0.19 = 52.6.
        List<FrameSample> stream = FromFrameTimes([100, 10, 10, 10, 10, 10, 10, 10, 10, 10]);

        FrameTimeSeries s = FrameTimeSeries.From(stream, null, Frequency);

        s.AverageFps.Should().BeApproximately(10 / 0.19, 0.01);
        s.AverageFps.Should().BeLessThan(91, "a slow frame costs its share of the wall clock, not its share of the count");
    }

    [Fact]
    public void AGapExcludesTheIntervalThatSpansItInsteadOfCountingALongFrame()
    {
        // A torn or overwritten slot between sample 1 and sample 2: the reader knows a record is missing, so
        // the 30 ms between the two survivors is two unknown frames, not one long one.
        List<FrameSample> stream = FromFrameTimes([10, 30, 10]);

        FrameTimeSeries s = FrameTimeSeries.From(stream, new HashSet<int> { 2 }, Frequency);

        s.FrameTimesMs.Should().Equal(10, 10);
        s.EndingSample.Should().Equal(1, 3);
        s.ExcludedForGaps.Should().Be(1);
        s.DurationSeconds.Should().BeApproximately(0.05, 1e-9, "the session's wall clock still spans the gap");

        // beta.8: the average is over the time that was measured — a pause is a gap since then, and its minutes held no frame.
        s.MeasuredSeconds.Should().BeApproximately(0.02, 1e-9);
        s.AverageFps.Should().BeApproximately(100, 1e-6);
    }

    [Fact]
    public void AnIntervalTheClockDidNotAdvanceAcrossIsNotAFrameTime()
    {
        List<FrameSample> stream = FromFrameTimes([10, 10]);
        stream.Add(Present(stream[0].Qpc));    // a lapped drain returned an older record last

        FrameTimeSeries s = FrameTimeSeries.From(stream, null, Frequency);

        s.FrameTimesMs.Should().Equal(10, 10);
        s.ExcludedNonPositive.Should().Be(1);
        s.DurationSeconds.Should().Be(0, "first to last present is negative here, and a negative duration is clamped rather than signed");
    }

    [Fact]
    public void FewerThanTwoPresentsHaveNoIntervalAndNoAverage()
    {
        FrameTimeSeries one = FrameTimeSeries.From([Present(5)], null, Frequency);
        FrameTimeSeries none = FrameTimeSeries.From([], null, Frequency);

        one.Count.Should().Be(0);
        one.AverageFps.Should().BeNull();
        one.DurationSeconds.Should().Be(0);
        none.Presents.Should().Be(0);
        none.AverageFps.Should().BeNull();
    }

    [Fact]
    public void AZeroFrequencyIsRefused() =>
        FluentActions.Invoking(() => FrameTimeSeries.From(Stream(2), null, 0)).Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void ApplicationFramesAreTheIntervalsBetweenThePresentsThatCarriedOne()
    {
        // ×3: every third present drained the application frame's token.
        List<FrameSample> stream = FgStream(appFrames: 5, k: 3);

        FrameTimeSeries s = FrameTimeSeries.ApplicationFrames(stream, null, Frequency);

        s.FrameTimesMs.Should().HaveCount(4).And.AllSatisfy(ft => ft.Should().BeApproximately(3 * Step * 1000.0 / Frequency, 1e-9));
        s.EndingSample.Should().Equal(3, 6, 9, 12);
        s.Presents.Should().Be(5, "the application-frame presents, not every present");
        s.DurationSeconds.Should().BeApproximately(12.0 * Step / Frequency, 1e-9);
        s.ExcludedForGaps.Should().Be(0);
    }

    /// <summary>
    /// The measured shape (the owner's Onimusha demo capture, DLSS FG ×3, 2026-09-25): each generated frame is submitted half
    /// a millisecond after its application frame. Over presents the median frame is half a millisecond — 2,000 FPS on a
    /// 60 FPS game; application frame to application frame is the 16.7 ms the game ran at.
    /// </summary>
    [Fact]
    public void GeneratedPresentsSubmittedInABurstDoNotShortenTheApplicationFrame()
    {
        var stream = new List<FrameSample>();
        for (int f = 0; f < 40; f++)
        {
            ulong app = 1_000_000 + (ulong)(f * 16_667);
            stream.Add(Counted(app, tokens: 1));
            stream.Add(Counted(app + 500, tokens: 0));
            stream.Add(Counted(app + 1_000, tokens: 0));
        }

        FrameTimeSeries presents = FrameTimeSeries.From(stream, null, Frequency);
        FrameTimeSeries application = FrameTimeSeries.ApplicationFrames(stream, null, Frequency);

        FrameStatistics.From(presents.FrameTimesMs, presented: true).MedianFps.Should().BeApproximately(2000, 1);
        FrameStatistics.From(application.FrameTimesMs, presented: false).MedianFps.Should().BeApproximately(60, 0.01);
        application.Count.Should().Be(39);
    }

    [Fact]
    public void AGapInsideAnApplicationFrameExcludesThatIntervalOnly()
    {
        List<FrameSample> stream = FgStream(appFrames: 4, k: 2);

        // The gap sits before sample 5, a generated present between application frames 4 and 6.
        FrameTimeSeries s = FrameTimeSeries.ApplicationFrames(stream, new HashSet<int> { 5 }, Frequency);

        s.EndingSample.Should().Equal(2, 4);
        s.ExcludedForGaps.Should().Be(1);
        s.Presents.Should().Be(4);
    }

    [Fact]
    public void ASampleThatDoesNotClaimTheCountBreaksTheChain()
    {
        // Two uncounted presents lead (the hook installed late), and one in the middle: nobody said whether it was generated.
        List<FrameSample> counted = FgStream(appFrames: 4, k: 2, startQpc: 3_000_000);
        List<FrameSample> stream = [Present(1_000_000), Present(2_000_000), .. counted];
        stream[5] = stream[5] with { Measured = MeasuredFields.OutputRes | MeasuredFields.PresentArgs };

        FrameTimeSeries s = FrameTimeSeries.ApplicationFrames(stream, null, Frequency);

        s.EndingSample.Should().Equal(4, 8);    // sample 6's interval spans the uncounted sample 5
        s.ExcludedForGaps.Should().Be(1);
        s.Presents.Should().Be(4, "the leading uncounted presents are not application frames");
    }

    [Fact]
    public void AStreamThatCountedNoApplicationFrameHasNoSeries()
    {
        List<FrameSample> stream = [.. FgStream(appFrames: 3, k: 2).Select(static s => s with { FgEvaluations = 0 })];

        FrameTimeSeries s = FrameTimeSeries.ApplicationFrames(stream, null, Frequency);

        s.Count.Should().Be(0);
        s.Presents.Should().Be(0);
        s.DurationSeconds.Should().Be(0);
        s.AverageFps.Should().BeNull();
    }

    [Fact]
    public void AnApplicationFrameTheClockDidNotAdvanceToIsNotAFrameTime()
    {
        List<FrameSample> stream = FgStream(appFrames: 3, k: 1);
        stream.Add(stream[0]);    // a lapped drain returned an older record last

        FrameTimeSeries s = FrameTimeSeries.ApplicationFrames(stream, null, Frequency);

        s.Count.Should().Be(2);
        s.ExcludedNonPositive.Should().Be(1);
    }

    [Fact]
    public void ApplicationFramesRefuseAZeroFrequency() =>
        FluentActions.Invoking(() => FrameTimeSeries.ApplicationFrames(FgStream(2, 2), null, 0)).Should().Throw<ArgumentOutOfRangeException>();

    private static FrameSample Counted(ulong qpc, byte tokens) => new()
    {
        Qpc = qpc,
        SwapchainId = 1,
        Measured = MeasuredFields.OutputRes | MeasuredFields.PresentArgs | MeasuredFields.Fg | MeasuredFields.FgCounts,
        FgEvaluations = tokens,
        FgMode = FgKind.DlssG,
    };
}
