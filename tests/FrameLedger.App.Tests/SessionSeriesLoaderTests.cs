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
        // the first present, the generated one and the one after a gap are not application frames
        s.AppPresentIndex.Should().Equal(_appFrames);
        s.AppFrameTimesMs.Should().Equal(10, 10, 40);
        s.Stutter.Should().BeNull("three frames is under the 19-frame window, so the detector answers null rather than a guess");
        s.Segments.Select(static g => g.StartS).Should().Equal(0, 0.03);
        s.Segments[0].Label.Should().StartWith("DLSS");
        SensorSeries gpu = s.Sensors.Should().ContainSingle().Which;
        gpu.Name.Should().Be("gpu_temp");
        gpu.TimesS.Should().Equal(0, 2);
        gpu.Values.Should().Equal(60, 62);   // −1 is a tick with no reading, dropped
        s.PsoCreated![5].Should().Be(3);
    }

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
