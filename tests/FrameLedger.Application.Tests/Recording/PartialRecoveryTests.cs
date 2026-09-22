using FluentAssertions;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Sessions;
using FrameLedger.Shared;

namespace FrameLedger.Application.Tests.Recording;

/// <summary><c>04_CAPTURE</c>: <c>Interrupted → recovered from .partial</c>, and the three ways a file is dropped instead.</summary>
public sealed class PartialRecoveryTests
{
    private static PartialHeader Header(Guid guid, CaptureTier tier = CaptureTier.Hooked) => new()
    {
        SessionGuid = guid,
        StartedAt = SessionFixtures.StartedAt,
        QpcEpoch = SessionFixtures.QpcEpoch,
        QpcFrequency = SessionFixtures.QpcFrequency,
        GameId = 7,
        SnapshotId = 3,
        ExePath = @"C:\Games\Title\game.exe",
        Tier = tier,
        Mode = CaptureMode.Launch,
        OverlayBuildId = "abc",
        TelemetryDescriptor = "l1+lhm",
        LaunchWaitMs = 900,
    };

    private static (PartialRecovery Recovery, FakePartialSessionStore Store, FakeSessionRepository Repo) Make()
    {
        var store = new FakePartialSessionStore();
        var repo = new FakeSessionRepository();
        return (new PartialRecovery(store, new SessionFinalizer(repo, new RawSeriesCodec())), store, repo);
    }

    [Fact]
    public async Task AHookedFileWithAFlushBecomesAnInterruptedSessionFromItsPrefix()
    {
        (PartialRecovery recovery, FakePartialSessionStore store, FakeSessionRepository repo) = Make();
        var guid = Guid.NewGuid();
        var entry = (FakePartialSessionStore.Entry)store.Create(Header(guid));
        entry.AppendNote(SessionFixtures.StartedAt, "started Launch");
        entry.AppendRecords(0, SessionFixtures.Stream(4_000, FlMeasured.OutputRes | FlMeasured.PresentArgs).ToArray());
        entry.AppendSensors(SessionFixtures.Sensors(40).ToArray());
        entry.AppendTick(new PartialTick(400, 400, 3, 1, 2, SessionFixtures.StartedAt.AddSeconds(41).ToUnixTimeMilliseconds(),
            new FlWriterState { Status = 1, HooksInstalledMask = 1, FaultCount = 0 }));
        entry.Truncated = true;

        IReadOnlyList<RecoveryOutcome> outcomes = await recovery.RecoverAsync(TestContext.Current.CancellationToken);

        outcomes.Should().ContainSingle();
        outcomes[0].Status.Should().Be(RecoveryStatus.Recovered);
        outcomes[0].Detail.Should().Contain("4000 record(s)").And.Contain("truncated");
        FrameLedger.Application.Persistence.SessionRow row = repo.Stored.Single().Row;
        row.SessionGuid.Should().Be(guid);
        row.ExitStatus.Should().Be(ExitStatus.Interrupted);
        row.Tier.Should().Be(CaptureTier.Hooked);
        row.EndedAt.Should().Be(SessionFixtures.StartedAt.AddSeconds(41), "the last flush is the last moment the session is known to have run");
        row.CaptureNotes.Should().Contain("recovered from .partial").And.Contain("last note: started Launch");
        row.FrameCount.Should().Be(4_000);
        row.DroppedRecords.Should().Be(3);
        row.LaunchWaitMs.Should().Be(900);
        row.OverlayBuildId.Should().Be("abc");
        row.AvgGpuTemp.Should().NotBeNull();
        store.Files.Should().BeEmpty("the file is gone once the row is in");
    }

    [Fact]
    public async Task AFileWithNoFlushEndsAtItsLastRecordAndAnEmptyOneIsDiscarded()
    {
        (PartialRecovery recovery, FakePartialSessionStore store, FakeSessionRepository repo) = Make();
        var empty = Guid.NewGuid();
        var records = Guid.NewGuid();
        store.Create(Header(empty)).Dispose();
        var entry = (FakePartialSessionStore.Entry)store.Create(Header(records));
        entry.AppendRecords(0, SessionFixtures.Stream(3_500, FlMeasured.OutputRes | FlMeasured.PresentArgs).ToArray());    // 35 s of presents from +1 s

        IReadOnlyList<RecoveryOutcome> outcomes = await recovery.RecoverAsync(TestContext.Current.CancellationToken);

        outcomes.Should().HaveCount(2);
        outcomes.Single(o => o.SessionGuid == empty).Status.Should().Be(RecoveryStatus.Discarded);
        outcomes.Single(o => o.SessionGuid == records).Status.Should().Be(RecoveryStatus.Recovered);
        repo.Stored.Single().Row.DurationSeconds.Should().BeApproximately(1 + 3_499 * 0.01, 0.01);
        store.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task AFileWhoseSessionWasAlreadyStoredIsOnlyRemoved()
    {
        (PartialRecovery recovery, FakePartialSessionStore store, FakeSessionRepository repo) = Make();
        var guid = Guid.NewGuid();
        repo.PreExisting.Add(guid);
        var entry = (FakePartialSessionStore.Entry)store.Create(Header(guid));
        entry.AppendTick(new PartialTick(1, 1, 0, 0, 1, SessionFixtures.StartedAt.AddSeconds(60).ToUnixTimeMilliseconds(), default));

        IReadOnlyList<RecoveryOutcome> outcomes = await recovery.RecoverAsync(TestContext.Current.CancellationToken);

        outcomes.Single().Status.Should().Be(RecoveryStatus.AlreadyStored);
        repo.Stored.Should().BeEmpty();
        store.Deleted.Should().Equal(guid);
    }

    [Fact]
    public async Task ATierTwoFileRecoversAsATierTwoRow()
    {
        (PartialRecovery recovery, FakePartialSessionStore store, FakeSessionRepository repo) = Make();
        var guid = Guid.NewGuid();
        var entry = (FakePartialSessionStore.Entry)store.Create(Header(guid, CaptureTier.NotHooked));
        entry.AppendSensors(SessionFixtures.Sensors(45).ToArray());    // no tick, no record: nothing was ever drained

        await recovery.RecoverAsync(TestContext.Current.CancellationToken);

        FrameLedger.Application.Persistence.SessionRow row = repo.Stored.Single().Row;
        row.Tier.Should().Be(CaptureTier.NotHooked);
        row.EndedAt.Should().Be(SessionFixtures.StartedAt.AddSeconds(44), "the last sensor sample is the only clock a Tier-2 file kept");
        row.FrameCount.Should().Be(0);
        row.PresentedFps.Should().BeNull();
        repo.Stored.Single().Sensors.Should().NotBeEmpty();
    }

    /// <summary>A file with a minute of Tier-2 sensors: long enough to be kept if it has an owner.</summary>
    private static Guid TierTwoFile(FakePartialSessionStore store, string exePath = @"C:\Games\Title\game.exe")
    {
        var guid = Guid.NewGuid();
        var entry = (FakePartialSessionStore.Entry)store.Create(Header(guid, CaptureTier.NotHooked) with { ExePath = exePath });
        entry.AppendSensors(SessionFixtures.Sensors(60).ToArray());
        return guid;
    }

    /// <summary>
    /// The owner's two GIRLS' FRONTLINE 2 files (2026-09-22 23:26, 2026-09-23 00:25): the entry was removed while the game
    /// ran. Its executable's path is now held by the re-imported entry, so the session is stored there.
    /// </summary>
    [Fact]
    public async Task AFileWhoseEntryWasRemovedAndReimportedIsStoredUnderTheRowThatHoldsItsExecutable()
    {
        (PartialRecovery recovery, FakePartialSessionStore store, FakeSessionRepository repo) = Make();
        repo.Owner = static (_, path, _) => path is not null ? 99 : null;
        Guid guid = TierTwoFile(store);

        RecoveryOutcome outcome = (await recovery.RecoverAsync(TestContext.Current.CancellationToken)).Single();

        outcome.Status.Should().Be(RecoveryStatus.Recovered);
        outcome.Detail.Should().Contain("games row 99").And.Contain(@"C:\Games\Title\game.exe");
        repo.Stored.Single().Row.GameId.Should().Be(99);
        repo.OwnerLookups.Should().Equal((7L, @"C:\Games\Title\game.exe"));
        store.Deleted.Should().Equal(guid);
    }

    [Fact]
    public async Task AFileWhoseGameWasRemovedWithItsSessionsIsDroppedAndSaysWhy()
    {
        (PartialRecovery recovery, FakePartialSessionStore store, FakeSessionRepository repo) = Make();
        repo.Owner = static (_, _, _) => null;
        Guid guid = TierTwoFile(store);

        RecoveryOutcome outcome = (await recovery.RecoverAsync(TestContext.Current.CancellationToken)).Single();

        outcome.Status.Should().Be(RecoveryStatus.GameRemoved);
        outcome.Detail.Should().Contain("games row 7").And.Contain("removed");
        repo.Stored.Should().BeEmpty();
        store.Deleted.Should().Equal(guid);
        store.Files.Should().BeEmpty();
    }

    /// <summary>
    /// Recovery runs before the watcher on every start, and a background service that throws stops the Agent: one file
    /// that cannot be finalized must be set aside, and the next one recovered, rather than stop every start after it.
    /// </summary>
    [Fact]
    public async Task AFileThatCannotBeFinalizedIsSetAsideAndTheNextIsStillRecovered()
    {
        (PartialRecovery recovery, FakePartialSessionStore store, FakeSessionRepository repo) = Make();
        const string poisoned = @"C:\Games\Broken\game.exe";
        repo.Owner = static (gameId, path, _) => string.Equals(path, poisoned, StringComparison.Ordinal) ? throw new InvalidOperationException("the ledger said no") : gameId;
        Guid bad = TierTwoFile(store, poisoned);
        Guid good = TierTwoFile(store);

        IReadOnlyList<RecoveryOutcome> outcomes = await recovery.RecoverAsync(TestContext.Current.CancellationToken);

        outcomes.Single(o => o.SessionGuid == bad).Status.Should().Be(RecoveryStatus.Failed);
        outcomes.Single(o => o.SessionGuid == bad).Detail.Should().Contain("the ledger said no").And.Contain(".partial.failed");
        outcomes.Single(o => o.SessionGuid == good).Status.Should().Be(RecoveryStatus.Recovered);
        store.Quarantined.Should().Equal(bad);
        store.Files.Should().BeEmpty("the bad file is set aside and the good one is stored");
        repo.Stored.Should().ContainSingle();
    }

    [Fact]
    public async Task AFolderThatCannotBeListedIsReportedNotThrown()
    {
        (PartialRecovery recovery, FakePartialSessionStore store, _) = Make();
        store.ListFailure = new IOException("access denied");

        IReadOnlyList<RecoveryOutcome> outcomes = await recovery.RecoverAsync(TestContext.Current.CancellationToken);

        outcomes.Should().ContainSingle().Which.Status.Should().Be(RecoveryStatus.Failed);
        outcomes[0].Detail.Should().Contain("access denied");
    }

    [Fact]
    public async Task RecoveryIsIdempotentOnAnEmptyStore()
    {
        (PartialRecovery recovery, _, _) = Make();

        IReadOnlyList<RecoveryOutcome> first = await recovery.RecoverAsync(TestContext.Current.CancellationToken);
        IReadOnlyList<RecoveryOutcome> second = await recovery.RecoverAsync(TestContext.Current.CancellationToken);

        first.Should().BeEmpty();
        second.Should().BeEmpty();
    }
}
