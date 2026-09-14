using Dapper;
using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Sessions;
using FrameLedger.Infrastructure.Blobs;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.Infrastructure.Tests.Persistence;

/// <summary>
/// Tools ▸ Database maintenance's App-side half (P4 PR-7): the integrity check answers SQLite's "ok", a backup is a
/// complete ledger that never overwrites a file and never targets the ledger itself, and compacting gives the space of
/// swept blobs back to the disk.
/// </summary>
public sealed class LedgerMaintenanceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly ExecutableFingerprint _game = new() { ExePath = @"C:\Games\M\m.exe", SizeBytes = 1, MtimeUnixMs = 2 };

    private static string TempFile() => Path.Combine(Path.GetTempPath(), "fl-backup-" + Guid.NewGuid().ToString("N") + ".db");

    private static SessionRow Row(long gameId, long snapshotId, DateTimeOffset started) => new()
    {
        SessionGuid = Guid.NewGuid(),
        GameId = gameId,
        SnapshotId = snapshotId,
        StartedAt = started,
        EndedAt = started.AddSeconds(90),
        QpcEpoch = 1,
        QpcFrequency = 10_000_000,
        Tier = CaptureTier.Hooked,
        Mode = CaptureMode.Launch,
        ExitStatus = ExitStatus.Normal,
        FrameCount = 5400,
        AppFrameCount = 5400,
        DisplayedFrameCount = 5400,
        DroppedFrames = 0,
        FgMode = "none",
        FgSource = "none",
        RtFlag = "no",
        RtSource = "measured",
        NativeFps = 60.0,
    };

    private static FrameBlobs Frames(int n) => new()
    {
        Codec = SeriesCodec.Tag,
        SampleCount = n,
        FrameTimes = SeriesCodec.EncodeFloat32([.. Enumerable.Range(0, n).Select(static i => 16f + (i % 97 / 10f))]),
        FrameFlags = SeriesCodec.EncodeBytes([.. Enumerable.Range(0, n).Select(static i => (byte)(i % 7))]),
        LatencyUs = SeriesCodec.EncodeUInt32([.. Enumerable.Range(0, n).Select(static i => (uint)(12_000 + (i % 1_009)))]),
    };

    private static async Task<(long GameId, long SnapshotId)> SeedAsync(LedgerDatabase db)
    {
        long gameId = (await new SqliteGameRepository(db).EnsureAsync(_game, "M", Ct).ConfigureAwait(false)).Id;
        long snapshotId = await new SqliteHardwareSnapshotRepository(db).EnsureAsync(new HardwareSnapshot { GpuName = "GPU", CpuName = "CPU" }, DateTimeOffset.UnixEpoch, Ct).ConfigureAwait(false);
        return (gameId, snapshotId);
    }

    [Fact]
    public async Task AFreshLedgerIsHealthy()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();

        IReadOnlyList<string> lines = await new LedgerMaintenance(f.Db).CheckIntegrityAsync(Ct);

        lines.Should().Equal("ok");
        LedgerMaintenance.IsHealthy(lines).Should().BeTrue();
        LedgerMaintenance.IsHealthy(["*** in database main ***", "Page 7 is never used"]).Should().BeFalse();
    }

    [Fact]
    public async Task ABackupIsACompleteLedgerWithTheSameRows()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        (long gameId, long snapshotId) = await SeedAsync(f.Db);
        await new SqliteSessionRepository(f.Db).InsertFinalizedAsync(new FinalizedSession { Row = Row(gameId, snapshotId, DateTimeOffset.UnixEpoch), Frames = Frames(100) }, Ct);
        string backup = TempFile();
        try
        {
            await new LedgerMaintenance(f.Db).BackupAsync(backup, Ct);

            LedgerDatabase copy = await LedgerDatabase.OpenAsync(backup, ct: Ct);
            await using (copy)
            {
                copy.SchemaVersion.Should().Be(f.Db.SchemaVersion);
                copy.Migration.Should().Be(MigrationOutcome.AlreadyCurrent, "the copy is already at the ledger's schema");
                long sessions = await copy.ReadAsync((c, ct) => c.ExecuteScalarAsync<long>(new CommandDefinition("SELECT COUNT(*) FROM sessions", cancellationToken: ct)), Ct);
                long frames = await copy.ReadAsync((c, ct) => c.ExecuteScalarAsync<long>(new CommandDefinition("SELECT COUNT(*) FROM frame_blobs", cancellationToken: ct)), Ct);
                (sessions, frames).Should().Be((1L, 1L));
                LedgerMaintenance.IsHealthy(await new LedgerMaintenance(copy).CheckIntegrityAsync(Ct)).Should().BeTrue();
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(backup);
        }
    }

    [Fact]
    public async Task ABackupNeverOverwritesAFileAndNeverTargetsTheLedgerItself()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var maintenance = new LedgerMaintenance(f.Db);
        string existing = TempFile();
        await File.WriteAllTextAsync(existing, "someone's file", Ct);
        try
        {
            Func<Task> overwrite = async () => await maintenance.BackupAsync(existing, Ct).ConfigureAwait(false);
            await overwrite.Should().ThrowAsync<IOException>();
            (await File.ReadAllTextAsync(existing, Ct)).Should().Be("someone's file");
        }
        finally
        {
            File.Delete(existing);
        }

        foreach (string ledgerFile in new[] { f.Path, f.Path + "-wal", f.Path.ToUpperInvariant() })
        {
            Func<Task> self = async () => await maintenance.BackupAsync(ledgerFile, Ct).ConfigureAwait(false);
            (await self.Should().ThrowAsync<IOException>()).Which.Message.Should().Contain("the ledger itself");
        }

        LedgerMaintenance.IsHealthy(await maintenance.CheckIntegrityAsync(Ct)).Should().BeTrue("nothing touched the ledger");
    }

    [Fact]
    public async Task CompactingGivesTheSpaceOfSweptBlobsBackToTheDisk()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        (long gameId, long snapshotId) = await SeedAsync(f.Db);
        var sessions = new SqliteSessionRepository(f.Db);
        for (int i = 0; i < 12; i++)
        {
            await sessions.InsertFinalizedAsync(new FinalizedSession { Row = Row(gameId, snapshotId, DateTimeOffset.UnixEpoch.AddHours(i)), Frames = Frames(40_000) }, Ct);
        }

        RetentionSweepResult swept = await sessions.SweepRetentionAllAsync(keep: 2, Ct);
        swept.Sessions.Should().Be(10);

        LedgerCompaction result = await new LedgerMaintenance(f.Db).CompactAsync(Ct);

        result.BytesAfter.Should().BeLessThan(result.BytesBefore / 2, "ten of twelve raw series were swept, and VACUUM plus a truncating checkpoint returns their pages");
        LedgerMaintenance.IsHealthy(await new LedgerMaintenance(f.Db).CheckIntegrityAsync(Ct)).Should().BeTrue();
        (await sessions.ListRecentAsync(20, Ct)).Should().HaveCount(12, "compacting changes no row");
    }
}
