using System.Text.Json;
using Dapper;
using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.TriState;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Metrics;
using FrameLedger.Domain.Sessions;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.Infrastructure.Tests.Persistence;

/// <summary>
/// The <c>games</c> adapter (untested since P2 PR-B; P3 PR-3): hooking off on creation, the UI's metadata
/// write with its provenance rule, the FR-8.3 defaults, and FR-1.4's two removals.
/// </summary>
public sealed class SqliteGameRepositoryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly ExecutableFingerprint _exe = new() { ExePath = @"C:\Games\T\t.exe", SizeBytes = 10, MtimeUnixMs = 20 };

    private static async Task<long> AddSessionAsync(LedgerFixture f, long gameId)
    {
        long snapshotId = await new SqliteHardwareSnapshotRepository(f.Db).EnsureAsync(new HardwareSnapshot { GpuName = "G" }, DateTimeOffset.UnixEpoch, Ct).ConfigureAwait(false);
        return await new SqliteSessionRepository(f.Db).InsertFinalizedAsync(new FinalizedSession
        {
            Row = new SessionRow
            {
                SessionGuid = Guid.NewGuid(),
                GameId = gameId,
                SnapshotId = snapshotId,
                StartedAt = DateTimeOffset.UnixEpoch,
                EndedAt = DateTimeOffset.UnixEpoch.AddSeconds(60),
                QpcEpoch = 0,
                QpcFrequency = 1,
                Tier = CaptureTier.NotHooked,
                Mode = CaptureMode.Attach,
                ExitStatus = ExitStatus.Normal,
            },
        }, Ct).ConfigureAwait(false);
    }

    [Fact]
    public async Task ANewGameIsHookingOffWithEveryDefaultAndEnsureIsIdempotent()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);

        GameRow row = await repo.EnsureAsync(_exe, "T", Ct);
        row.HookEnabled.Should().BeFalse("CLAUDE.md rule 1");
        row.HookPrescanState.Should().Be("not_run");
        row.Platform.Should().Be("none");
        row.RtDefault.Should().Be(Tri.NotApplicable);
        row.InLibrary.Should().BeTrue();
        row.FieldProvenanceJson.Should().BeNull();

        (await repo.EnsureAsync(_exe, "Other name", Ct)).Id.Should().Be(row.Id, "the path is the identity");
        (await repo.FindByIdAsync(row.Id, Ct))!.Name.Should().Be("T");
        (await repo.FindByIdAsync(row.Id + 99, Ct)).Should().BeNull();
        (await repo.ListAsync(Ct)).Should().ContainSingle();
    }

    [Fact]
    public async Task AUserEditMarksOnlyTheChangedFieldsAsUserAndTouchesNoHookColumn()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);
        GameRow row = await repo.EnsureAsync(_exe, "T", Ct);
        // A detection pass (P4) had badged two fields; the row's hook state is the consent store's.
        await f.Db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition(
            "UPDATE games SET engine = 'Unreal', publisher = 'Pub', field_provenance = '{\"engine\":\"detected\",\"publisher\":\"detected\"}', hook_prescan_state = 'clean' WHERE id = @id",
            new { id = row.Id }, tx, cancellationToken: ct)), Ct);

        bool ok = await repo.UpdateMetadataAsync(row.Id, new GameMetadata { Name = "Title", Platform = "steam", StoreId = "123", Engine = "Unreal", Publisher = "Real Pub", Notes = "n" }, Ct);
        ok.Should().BeTrue();

        GameRow after = (await repo.FindByIdAsync(row.Id, Ct))!;
        after.Name.Should().Be("Title");
        after.Platform.Should().Be("steam");
        after.StoreId.Should().Be("123");
        after.Publisher.Should().Be("Real Pub");
        after.Notes.Should().Be("n");
        after.HookPrescanState.Should().Be("clean", "the UI's write never touches a hook-state column");
        after.HookEnabled.Should().BeFalse();
        JsonSerializer.Deserialize<Dictionary<string, string>>(after.FieldProvenanceJson!).Should().BeEquivalentTo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["engine"] = "detected",   // unchanged: keeps its badge
            ["publisher"] = "user",    // changed: the user's now, and detection must not overwrite it
            ["name"] = "user",
            ["platform"] = "user",
            ["store_id"] = "user",
        });

        (await repo.UpdateMetadataAsync(row.Id + 99, new GameMetadata { Name = "x" }, Ct)).Should().BeFalse();
        Func<Task> badPlatform = async () => await repo.UpdateMetadataAsync(row.Id, new GameMetadata { Name = "x", Platform = "origin" }, Ct).ConfigureAwait(true);
        await badPlatform.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TheTriStateDefaultsAreWrittenPerKind()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);
        GameRow row = await repo.EnsureAsync(_exe, "T", Ct);

        (await repo.SetTriStateDefaultAsync(row.Id, TriStateKind.PathTracing, Tri.Yes, Ct)).Should().BeTrue();
        (await repo.SetTriStateDefaultAsync(row.Id, TriStateKind.RayReconstruction, Tri.No, Ct)).Should().BeTrue();
        GameRow after = (await repo.FindByIdAsync(row.Id, Ct))!;
        (after.RtDefault, after.PtDefault, after.RrDefault).Should().Be((Tri.NotApplicable, Tri.Yes, Tri.No));
        (await f.Db.ReadAsync((c, ct) => c.ExecuteScalarAsync<string>(new CommandDefinition("SELECT pt_default FROM games", cancellationToken: ct)), Ct)).Should().Be("yes");
    }

    [Fact]
    public async Task RemovingWithSessionsKeptHidesTheGameKeepsItsRowsAndALaunchRestoresIt()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);
        GameRow row = await repo.EnsureAsync(_exe, "T", Ct);
        long sessionId = await AddSessionAsync(f, row.Id);

        (await repo.RemoveAsync(row.Id, keepSessions: true, Ct)).Should().BeTrue();
        (await repo.ListAsync(Ct)).Should().BeEmpty("removed from the library");
        (await repo.FindByIdAsync(row.Id, Ct))!.InLibrary.Should().BeFalse();
        (await new SqliteSessionRepository(f.Db).FindByIdAsync(sessionId, Ct)).Should().NotBeNull("the sessions were kept");
        (await repo.RemoveAsync(row.Id, keepSessions: true, Ct)).Should().BeFalse("already removed");

        GameRow back = await repo.EnsureAsync(_exe, "T", Ct);
        back.Id.Should().Be(row.Id);
        back.InLibrary.Should().BeTrue("the watcher saw it run again");
        (await repo.ListAsync(Ct)).Should().ContainSingle();
    }

    [Fact]
    public async Task RemovingWithSessionsDeletedCascadesEverythingUnderTheGame()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);
        GameRow row = await repo.EnsureAsync(_exe, "T", Ct);
        long sessionId = await AddSessionAsync(f, row.Id);
        await new SqliteSessionAnnotationRepository(f.Db).UpsertAsync(new SessionAnnotation { SessionId = sessionId, Notes = "bye" }, Ct);

        (await repo.RemoveAsync(row.Id, keepSessions: false, Ct)).Should().BeTrue();
        (await repo.FindByIdAsync(row.Id, Ct)).Should().BeNull();
        (await new SqliteSessionRepository(f.Db).FindByIdAsync(sessionId, Ct)).Should().BeNull();
        (await new SqliteSessionAnnotationRepository(f.Db).FindAsync(sessionId, Ct)).Should().BeNull();
        (await repo.RemoveAsync(row.Id, keepSessions: false, Ct)).Should().BeFalse();
    }
}
