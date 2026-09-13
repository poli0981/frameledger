using System.Diagnostics;
using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Sessions;
using FrameLedger.Infrastructure.Blobs;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.Infrastructure.Tests.Persistence;

/// <summary>The UI's reads (P3 PR-3): by game newest first, by id, the per-game summary, segments and sensors — and NFR-4's 500 ms over a 100-session game.</summary>
public sealed class SqliteSessionQueryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<long> GameAsync(LedgerFixture f, string exe) =>
        (await new SqliteGameRepository(f.Db).EnsureAsync(new ExecutableFingerprint { ExePath = exe, SizeBytes = 1, MtimeUnixMs = 1 }, Path.GetFileName(exe), Ct).ConfigureAwait(false)).Id;

    private static SessionRow Row(long gameId, long snapshotId, int index, CaptureTier tier = CaptureTier.Hooked) => new()
    {
        SessionGuid = Guid.NewGuid(),
        GameId = gameId,
        SnapshotId = snapshotId,
        StartedAt = DateTimeOffset.UnixEpoch.AddDays(index),
        EndedAt = DateTimeOffset.UnixEpoch.AddDays(index).AddSeconds(90 + index),
        QpcEpoch = 0,
        QpcFrequency = 10_000_000,
        Tier = tier,
        Mode = CaptureMode.Launch,
        ExitStatus = ExitStatus.Normal,
        FrameCount = tier == CaptureTier.Hooked ? 5400 : 0,
        NativeFps = tier == CaptureTier.Hooked ? 60 + index : null,
    };

    [Fact]
    public async Task ByGameIsNewestFirstAndBoundedAndTheSummaryCountsPerGame()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        long a = await GameAsync(f, @"C:\a\a.exe");
        long b = await GameAsync(f, @"C:\b\b.exe");
        long snapshotId = await new SqliteHardwareSnapshotRepository(f.Db).EnsureAsync(new HardwareSnapshot { GpuName = "G" }, DateTimeOffset.UnixEpoch, Ct);
        var repo = new SqliteSessionRepository(f.Db);
        for (int i = 0; i < 5; i++)
        {
            await repo.InsertFinalizedAsync(new FinalizedSession { Row = Row(a, snapshotId, i, i == 0 ? CaptureTier.NotHooked : CaptureTier.Hooked) }, Ct);
        }

        long bId = await repo.InsertFinalizedAsync(new FinalizedSession { Row = Row(b, snapshotId, 10) }, Ct);

        IReadOnlyList<SessionRow> ofA = await repo.ListByGameAsync(a, 3, Ct);
        ofA.Should().HaveCount(3).And.BeInDescendingOrder(static s => s.StartedAt).And.OnlyContain(s => s.GameId == a);
        (await repo.ListByGameAsync(b, 10, Ct)).Should().ContainSingle().Which.Id.Should().Be(bId);
        (await repo.FindByIdAsync(bId, Ct))!.GameId.Should().Be(b);
        (await repo.FindByIdAsync(bId + 99, Ct)).Should().BeNull();

        IReadOnlyList<GameSessionSummary> summary = await repo.SummariseByGameAsync(Ct);
        summary.Should().HaveCount(2);
        GameSessionSummary ofGameA = summary.Single(s => s.GameId == a);
        ofGameA.SessionCount.Should().Be(5);
        ofGameA.HookedCount.Should().Be(4);
        ofGameA.TotalSeconds.Should().Be(90 + 91 + 92 + 93 + 94);
        ofGameA.LastPlayedAt.Should().Be(DateTimeOffset.UnixEpoch.AddDays(4).AddSeconds(94));
    }

    [Fact]
    public async Task SegmentsAndSensorsComeBackInOrderAndStillEncoded()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        long a = await GameAsync(f, @"C:\a\a.exe");
        long snapshotId = await new SqliteHardwareSnapshotRepository(f.Db).EnsureAsync(new HardwareSnapshot { GpuName = "G" }, DateTimeOffset.UnixEpoch, Ct);
        var repo = new SqliteSessionRepository(f.Db);
        SegmentRow[] segments =
        [
            new() { SwapchainId = 1, StartFrame = 0, EndFrame = 999, RenderW = 1485, RenderH = 835, OutputW = 2560, OutputH = 1440, Upscaler = "dlss", UpscalerQuality = "quality", FgMode = "none", NativeFps = 60, DisplayedFps = 60, P1LowFps = 48 },
            new() { SwapchainId = 1, StartFrame = 1000, EndFrame = 5399, RenderW = 1280, RenderH = 720, OutputW = 2560, OutputH = 1440, Upscaler = "dlss", UpscalerQuality = "performance", FgMode = "dlssg", NativeFps = 70, DisplayedFps = 130, P1LowFps = 55 },
        ];
        SensorBlob[] sensors =
        [
            new() { Series = "gpu_temp", Hz = 1, Codec = SeriesCodec.Tag, Data = SeriesCodec.EncodeFloat32([60f, 61f]) },
            new() { Series = "cpu_temp", Hz = 1, Codec = SeriesCodec.Tag, Data = SeriesCodec.EncodeFloat32([-1f, 50f]) },
        ];
        long id = await repo.InsertFinalizedAsync(new FinalizedSession { Row = Row(a, snapshotId, 0), Segments = [segments[1], segments[0]], Sensors = sensors }, Ct);

        (await repo.FindSegmentsAsync(id, Ct)).Should().BeEquivalentTo(segments, o => o.WithStrictOrdering(), "frame order, whatever order they were written in");
        IReadOnlyList<SensorBlob> back = await repo.FindSensorsAsync(id, Ct);
        back.Select(static s => s.Series).Should().Equal("cpu_temp", "gpu_temp");
        SeriesCodec.DecodeFloat32(back[1].Data.ToArray()).Should().Equal(60f, 61f);
        (await repo.FindSegmentsAsync(id + 99, Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task AHundredSessionGameListsWithinNfr4()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        long a = await GameAsync(f, @"C:\a\a.exe");
        long snapshotId = await new SqliteHardwareSnapshotRepository(f.Db).EnsureAsync(new HardwareSnapshot { GpuName = "G" }, DateTimeOffset.UnixEpoch, Ct);
        var repo = new SqliteSessionRepository(f.Db);
        for (int i = 0; i < 100; i++)
        {
            await repo.InsertFinalizedAsync(new FinalizedSession { Row = Row(a, snapshotId, i) }, Ct);
        }

        var sw = Stopwatch.StartNew();
        IReadOnlyList<SessionRow> rows = await repo.ListByGameAsync(a, 100, Ct);
        IReadOnlyList<SessionAnnotation> annotations = await new SqliteSessionAnnotationRepository(f.Db).ListByGameAsync(a, Ct);
        sw.Stop();

        rows.Should().HaveCount(100);
        annotations.Should().BeEmpty();
        // NFR-4: "a game with 100 sessions opens in ≤ 500 ms". Measured on the dev box: a few ms; the ceiling is the requirement.
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(500), $"NFR-4 (measured {sw.ElapsedMilliseconds} ms)");
    }
}
