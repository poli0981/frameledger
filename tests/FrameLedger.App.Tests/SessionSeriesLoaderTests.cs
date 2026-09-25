using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.Application.Persistence;
using FrameLedger.Infrastructure.Blobs;

namespace FrameLedger.App.Tests;

/// <summary>The blobs decoded for the charts: cumulative time, the generated and gap bits, the application-frame view with stutter flags, segments on the time axis, sensors aligned to <c>t_ms</c>.</summary>
public sealed class SessionSeriesLoaderTests
{
    private static readonly int[] _appFrames = [1, 3, 5];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void DecodeBuildsTheViewsFromTheBlobs()
    {
        // 6 presents at 10 ms; the third is generated (DLSS-G); the fifth follows a gap (interval 0, excluded); the sixth stutters.
        float[] frametimes = [0, 10, 10, 10, 0, 40];
        byte[] flags = [4, 0, 1, 0, 4, 0];   // Gap = 1<<2, Generated = 1<<0 (Application.Recording.FrameFlagBits)
        var frames = new FrameBlobs
        {
            Codec = SeriesCodec.Tag,
            SampleCount = 6,
            FrameTimes = SeriesCodec.EncodeFloat32(frametimes),
            FrameFlags = SeriesCodec.EncodeBytes(flags),
            FrameIndex = SeriesCodec.EncodeUInt32([0, 1, 2, 3, 4, 5]),
            PsoCreated = SeriesCodec.EncodeUInt16([0, 0, 0, 0, 0, 3]),
        };
        SegmentRow[] segments = [new() { SwapchainId = 1, StartFrame = 0, EndFrame = 2, Upscaler = "dlss" }, new() { SwapchainId = 1, StartFrame = 3, EndFrame = 5, Upscaler = "fsr3" }];
        SensorBlob[] sensors =
        [
            new() { Series = "t_ms", Hz = 1, Codec = SeriesCodec.Tag, Data = SeriesCodec.EncodeFloat32([0, 1000, 2000]) },
            new() { Series = "gpu_temp", Hz = 1, Codec = SeriesCodec.Tag, Data = SeriesCodec.EncodeFloat32([60, -1, 62]) },
        ];

        SessionSeries s = SessionSeriesLoader.Decode(frames, segments, sensors);

        s.Presents.Should().Be(6);
        s.TimesS.Should().Equal(0, 0.01, 0.02, 0.03, 0.03, 0.07);
        s.Generated.Should().Equal(false, false, true, false, false, false);
        s.Gap.Should().Equal(true, false, false, false, true, false);
        s.HasGenerated.Should().BeTrue();
        // the first present, the generated one and the one after a gap end no application frame; the third application
        // frame's time spans the generated present before it (beta.8: application frame to application frame)
        s.AppPresentIndex.Should().Equal(_appFrames);
        s.AppFrameTimesMs.Should().Equal(10, 20, 40);
        s.NoApplicationFrames.Should().BeFalse();
        s.Stutter.Should().BeNull("three frames is under the 19-frame window, so the detector answers null rather than a guess");
        s.Segments.Select(static g => g.StartS).Should().Equal(0, 0.03);
        s.Segments[0].Label.Should().StartWith("DLSS");
        SensorSeries gpu = s.Sensors.Should().ContainSingle().Which;
        gpu.Name.Should().Be("gpu_temp");
        gpu.TimesS.Should().Equal(0, 2);
        s.SensorsAligned.Should().BeFalse("a blob written before schema 0013 does not say where its first present sits");
        gpu.Values.Should().Equal(60, 62);   // −1 is a tick with no reading, dropped
        s.PsoCreated![5].Should().Be(3);
    }

    /// <summary>
    /// beta.8, the owner's Onimusha capture (DLSS FG ×3): each generated frame is submitted half a millisecond after its
    /// application frame. The "native" series was each application frame's interval to the present before it — a generated
    /// frame's half millisecond. It is application frame to application frame now.
    /// </summary>
    [Fact]
    public void TheApplicationFramesTimeSpansTheGeneratedPresentsBeforeIt()
    {
        var frametimes = new List<float> { 0 };
        var flags = new List<byte> { 4 };
        for (int f = 0; f < 30; f++)
        {
            frametimes.AddRange([0.5f, 0.5f, 15.667f]);
            flags.AddRange([1, 1, 0]);
        }

        SessionSeries s = SessionSeriesLoader.Decode(Blob([.. frametimes], [.. flags]), [], []);

        s.AppFrameTimesMs.Should().HaveCount(30).And.AllSatisfy(static ft => ft.Should().BeApproximately(16.667, 0.001));
    }

    [Fact]
    public void EveryPresentGeneratedIsChartedAsPresentsAndSaysSo()
    {
        // The no_evaluations shape: frame generation counted and no application frame's token arrived.
        SessionSeries s = SessionSeriesLoader.Decode(Blob([0, 10, 10, 10], [5, 1, 1, 1]), [], []);

        s.NoApplicationFrames.Should().BeTrue();
        s.AppFrameTimesMs.Should().Equal(10, 10, 10);
    }

    /// <summary>beta.8: a session that presented to two swapchains is charted on the one its statistics are over, with that stream's segments.</summary>
    [Fact]
    public void OnlyTheDominantStreamIsCharted()
    {
        // Stream 7 (a launcher window) presents twice; stream 3 five times. Intervals are per stream, as the finalizer stores them.
        FrameBlobs frames = Blob([0, 0, 16, 100, 16, 16, 16], [4, 4, 0, 0, 0, 0, 0]) with
        {
            SwapchainIds = SeriesCodec.EncodeUInt32([7, 3, 3, 7, 3, 3, 3]),
            FrameIndex = SeriesCodec.EncodeUInt32([0, 1, 2, 3, 4, 5, 6]),
        };
        SegmentRow[] segments = [new() { SwapchainId = 7, StartFrame = 0, EndFrame = 3 }, new() { SwapchainId = 3, StartFrame = 1, EndFrame = 6 }];

        SessionSeries s = SessionSeriesLoader.Decode(frames, segments, []);

        s.Presents.Should().Be(5);
        s.FrameIndex.Should().Equal(1u, 2u, 4u, 5u, 6u);
        s.AppFrameTimesMs.Should().Equal(16, 16, 16, 16);
        s.Segments.Should().ContainSingle("stream 7's segment is not on this axis");
    }

    /// <summary>Schema 0013: the first present's place on the sensors' clock puts the sensors on the frames' axis — before it, negative.</summary>
    [Fact]
    public void SensorsAreOnTheFramesAxisWhenTheBlobSaysWhereTheFirstPresentIs()
    {
        SensorBlob[] sensors =
        [
            new() { Series = "t_ms", Hz = 1, Codec = SeriesCodec.Tag, Data = SeriesCodec.EncodeFloat32([0, 5000, 10000]) },
            new() { Series = "gpu_temp", Hz = 1, Codec = SeriesCodec.Tag, Data = SeriesCodec.EncodeFloat32([50, 60, 70]) },
        ];

        SessionSeries s = SessionSeriesLoader.Decode(Blob([0, 10, 10], [4, 0, 0]) with { FirstPresentMs = 5000 }, [], sensors);

        s.SensorsAligned.Should().BeTrue();
        s.Sensors.Should().ContainSingle().Which.TimesS.Should().Equal(-5, 0, 5);
    }

    [Fact]
    public async Task ATierTwoSessionsSensorsLoadOnTheirOwn()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        long id = await s.NotHookedWithSensorsAsync(game.Id, DateTimeOffset.UtcNow);

        IReadOnlyList<SensorSeries> sensors = await new SessionSeriesLoader(s.Sessions).LoadSensorsAsync(id, Ct);

        sensors.Select(static x => x.Name).Should().BeEquivalentTo("gpu_temp", "gpu_power");
        sensors[0].TimesS.Should().Equal(0.5, 1.5, 2.5);
    }

    private static FrameBlobs Blob(float[] frametimes, byte[] flags) => new()
    {
        Codec = SeriesCodec.Tag,
        SampleCount = frametimes.Length,
        FrameTimes = SeriesCodec.EncodeFloat32(frametimes),
        FrameFlags = SeriesCodec.EncodeBytes(flags),
    };

    [Fact]
    public async Task ATierTwoSessionOrOneWithoutABlobLoadsAsNull()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        long id = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow, hooked: false);
        (await new SessionSeriesLoader(s.Sessions).LoadAsync(id, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task ALongSessionRoundTripsThroughTheLedgerWithStutterFlags()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        long id = await s.SessionWithFramesAsync(game.Id, DateTimeOffset.UtcNow, frames: 2000, spikeEvery: 500);

        SessionSeries series = (await new SessionSeriesLoader(s.Sessions).LoadAsync(id, Ct))!;
        series.Presents.Should().Be(2000);
        series.Stutter.Should().NotBeNull();
        series.Stutter!.Count(static f => f).Should().Be(3, "one spike every 500 frames after the first");
        series.Segments.Should().ContainSingle();
        series.Sensors.Should().ContainSingle().Which.Name.Should().Be("gpu_temp");
    }
}
