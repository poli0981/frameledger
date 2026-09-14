using System.IO;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Sessions;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.App.Tests;

/// <summary>A scratch ledger per test with the App's own repositories over it — never the real one under %LOCALAPPDATA%.</summary>
internal sealed class ScratchLedger : IAsyncDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-app-" + Guid.NewGuid().ToString("N"));

    public LedgerDatabase Db { get; private set; } = null!;

    public SqliteGameRepository Games { get; private set; } = null!;

    public SqliteSessionRepository Sessions { get; private set; } = null!;

    public SqliteSessionAnnotationRepository Annotations { get; private set; } = null!;

    public GameLibrary Library { get; private set; } = null!;

    public static async Task<ScratchLedger> OpenAsync(TimeProvider? clock = null)
    {
        var s = new ScratchLedger();
        s.Db = await LedgerDatabase.OpenAsync(Path.Combine(s._dir, LedgerPaths.DatabaseFileName), ct: TestContext.Current.CancellationToken).ConfigureAwait(false);
        s.Games = new SqliteGameRepository(s.Db);
        s.Sessions = new SqliteSessionRepository(s.Db);
        s.Annotations = new SqliteSessionAnnotationRepository(s.Db);
        s.Library = new GameLibrary(s.Games, s.Sessions, s.Annotations, clock);
        return s;
    }

    public async Task<GameRow> GameAsync(string name, string exe = null!)
    {
        exe ??= Path.Combine(_dir, name + ".exe");
        return await Games.EnsureAsync(new ExecutableFingerprint { ExePath = exe, SizeBytes = 1, MtimeUnixMs = 1 }, name, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    public async Task<long> SessionAsync(long gameId, DateTimeOffset startedAt, int seconds = 600, bool hooked = true, string fgMode = "none", double? native = 60, double? displayed = null, double? factor = null, ExitStatus exit = ExitStatus.Normal)
    {
        long snapshotId = await new SqliteHardwareSnapshotRepository(Db).EnsureAsync(new HardwareSnapshot { GpuName = "G" }, startedAt, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var row = new SessionRow
        {
            SessionGuid = Guid.NewGuid(),
            GameId = gameId,
            SnapshotId = snapshotId,
            StartedAt = startedAt,
            EndedAt = startedAt.AddSeconds(seconds),
            QpcEpoch = 0,
            QpcFrequency = 1,
            Tier = hooked ? CaptureTier.Hooked : CaptureTier.NotHooked,
            Mode = CaptureMode.Launch,
            ExitStatus = exit,
            FrameCount = hooked ? seconds * 60 : 0,
            FgMode = hooked ? fgMode : "na",
            NativeFps = hooked ? native : null,
            DisplayedFps = displayed,
            FgFactor = factor,
            Api = hooked ? "d3d12" : null,
            Upscaler = hooked ? "dlss" : null,
            RenderW = hooked ? 1485 : null,
            RenderH = hooked ? 835 : null,
            OutputW = hooked ? 2560 : null,
            OutputH = hooked ? 1440 : null,
            RtFlag = hooked ? "yes" : "na",
            RtSource = hooked ? "measured" : null,
            P1LowFps = hooked ? 48 : null,
            MaxGpuTemp = 71,
        };
        return await Sessions.InsertFinalizedAsync(new FinalizedSession { Row = row }, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    /// <summary>A hooked session WITH blobs: <paramref name="frames"/> presents at 60 fps, a 4× spike every <paramref name="spikeEvery"/> frames, one segment, a gpu_temp series.</summary>
    public async Task<long> SessionWithFramesAsync(long gameId, DateTimeOffset startedAt, int frames, int spikeEvery)
    {
        long snapshotId = await new SqliteHardwareSnapshotRepository(Db).EnsureAsync(new HardwareSnapshot { GpuName = "G" }, startedAt, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var frametimes = new float[frames];
        var flags = new byte[frames];
        var index = new uint[frames];
        flags[0] = 4;   // the first present carries no interval (FrameFlagBits.Gap = 1 << 2)
        for (int i = 1; i < frames; i++)
        {
            frametimes[i] = spikeEvery > 0 && i % spikeEvery == 0 ? 66.667f : 16.667f;
            index[i] = (uint)i;
        }

        double seconds = frametimes.Sum() / 1000.0;
        SessionRow row = HookedRow(gameId, snapshotId, startedAt, frames, spikeEvery, seconds);
        return await Sessions.InsertFinalizedAsync(new FinalizedSession
        {
            Row = row,
            Segments = [new SegmentRow { SwapchainId = 1, StartFrame = 0, EndFrame = frames - 1, RenderW = 1485, RenderH = 835, OutputW = 2560, OutputH = 1440, Upscaler = "dlss", UpscalerQuality = "Quality", FgMode = "none", NativeFps = 60 }],
            Frames = new FrameBlobs
            {
                Codec = FrameLedger.Infrastructure.Blobs.SeriesCodec.Tag,
                SampleCount = frames,
                FrameTimes = FrameLedger.Infrastructure.Blobs.SeriesCodec.EncodeFloat32(frametimes),
                FrameFlags = FrameLedger.Infrastructure.Blobs.SeriesCodec.EncodeBytes(flags),
                FrameIndex = FrameLedger.Infrastructure.Blobs.SeriesCodec.EncodeUInt32(index),
            },
            Sensors =
            [
                new SensorBlob { Series = "t_ms", Hz = 1, Codec = FrameLedger.Infrastructure.Blobs.SeriesCodec.Tag, Data = FrameLedger.Infrastructure.Blobs.SeriesCodec.EncodeFloat32([0, 1000, 2000]) },
                new SensorBlob { Series = "gpu_temp", Hz = 1, Codec = FrameLedger.Infrastructure.Blobs.SeriesCodec.Tag, Data = FrameLedger.Infrastructure.Blobs.SeriesCodec.EncodeFloat32([60, 61, 62]) },
            ],
        }, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static SessionRow HookedRow(long gameId, long snapshotId, DateTimeOffset startedAt, int frames, int spikeEvery, double seconds) => new()
    {
        SessionGuid = Guid.NewGuid(),
        GameId = gameId,
        SnapshotId = snapshotId,
        StartedAt = startedAt,
        EndedAt = startedAt.AddSeconds(seconds),
        QpcEpoch = 0,
        QpcFrequency = 10_000_000,
        Tier = CaptureTier.Hooked,
        Mode = CaptureMode.Launch,
        ExitStatus = ExitStatus.Normal,
        FrameCount = frames,
        AppFrameCount = frames,
        DisplayedFrameCount = frames,
        FgMode = "none",
        FgSource = "none",
        NativeFps = 60,
        MedianFps = 60,
        P1LowFps = 48,
        P01LowFps = 40,
        MinFps = 15,
        MaxFps = 61,
        FrametimeStdDevMs = 1.5,
        StutterCount = spikeEvery > 0 ? (frames - 1) / spikeEvery : 0,
        StutterTimePct = 1.2,
        Api = "d3d12",
        PresentMode = "flip",
        Upscaler = "dlss",
        UpscalerQuality = "Quality",
        RenderW = 1485,
        RenderH = 835,
        OutputW = 2560,
        OutputH = 1440,
        RtFlag = "yes",
        RtSource = "measured",
        MaxGpuTemp = 71,
    };

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync().ConfigureAwait(false);
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
