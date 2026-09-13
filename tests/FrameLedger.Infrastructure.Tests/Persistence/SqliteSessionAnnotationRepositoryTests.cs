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
/// The UI-owned row beside a session: tags as a JSON array, notes, the three FR-8.3 overrides on THIS table
/// (schema 0002) and never on <c>sessions</c>; an empty annotation is a deleted row; and the resolution over
/// real rows.
/// </summary>
public sealed class SqliteSessionAnnotationRepositoryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(long GameId, long SessionId)> SeedAsync(LedgerFixture f, string rtFlag = "na")
    {
        long gameId = (await new SqliteGameRepository(f.Db).EnsureAsync(new ExecutableFingerprint { ExePath = @"C:\g\g.exe", SizeBytes = 1, MtimeUnixMs = 1 }, "G", Ct).ConfigureAwait(false)).Id;
        long snapshotId = await new SqliteHardwareSnapshotRepository(f.Db).EnsureAsync(new HardwareSnapshot { GpuName = "G" }, DateTimeOffset.UnixEpoch, Ct).ConfigureAwait(false);
        long sessionId = await new SqliteSessionRepository(f.Db).InsertFinalizedAsync(new FinalizedSession
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
                Tier = CaptureTier.Hooked,
                Mode = CaptureMode.Attach,
                ExitStatus = ExitStatus.Normal,
                RtFlag = rtFlag,
                RtSource = string.Equals(rtFlag, "na", StringComparison.Ordinal) ? null : "measured",
            },
        }, Ct).ConfigureAwait(false);
        return (gameId, sessionId);
    }

    [Fact]
    public async Task TagsNotesAndOverridesRoundTripAndAnEmptyAnnotationDeletesTheRow()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        (long gameId, long sessionId) = await SeedAsync(f);
        var repo = new SqliteSessionAnnotationRepository(f.Db);

        (await repo.FindAsync(sessionId, Ct)).Should().BeNull();
        (await repo.UpsertAsync(new SessionAnnotation { SessionId = sessionId + 99, Notes = "x" }, Ct)).Should().BeFalse("no such session");

        var a = new SessionAnnotation { SessionId = sessionId, Tags = ["benchmark", "1440p"], Notes = "clean run", RtOverride = Tri.Yes, RrOverride = Tri.NotApplicable };
        (await repo.UpsertAsync(a, Ct)).Should().BeTrue();
        (await repo.FindAsync(sessionId, Ct)).Should().BeEquivalentTo(a);
        (await repo.ListByGameAsync(gameId, Ct)).Should().ContainSingle().Which.Should().BeEquivalentTo(a);

        string? stored = await f.Db.ReadAsync((c, ct) => c.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT tags || '|' || rt_override || '|' || rr_override FROM session_annotations", cancellationToken: ct)), Ct);
        stored.Should().Be("[\"benchmark\",\"1440p\"]|yes|na", "06_DATA_MODEL: tags are a JSON array, overrides the tri-state text");
        string? measured = await f.Db.ReadAsync((c, ct) => c.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT rt_flag || '/' || IFNULL(rt_source, 'null') FROM sessions", cancellationToken: ct)), Ct);
        measured.Should().Be("na/null", "the override never touches the Agent's row");

        (await repo.UpsertAsync(new SessionAnnotation { SessionId = sessionId, Tags = [], Notes = "  " }, Ct)).Should().BeTrue();
        (await repo.FindAsync(sessionId, Ct)).Should().BeNull("nothing left to say is no row");
    }

    [Fact]
    public async Task AHandEditedTagsColumnReadsAsOneTagRatherThanARowNobodyCanOpen()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        (_, long sessionId) = await SeedAsync(f);
        await f.Db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition(
            "INSERT INTO session_annotations (session_id, tags) VALUES (@id, 'not json')", new { id = sessionId }, tx, cancellationToken: ct)), Ct);
        (await new SqliteSessionAnnotationRepository(f.Db).FindAsync(sessionId, Ct))!.Tags.Should().Equal("not json");
    }

    [Fact]
    public async Task TheResolutionOverStoredRowsFollowsFr83AndClearingAnOverrideRestoresTheMeasurement()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        (long gameId, long sessionId) = await SeedAsync(f, rtFlag: "yes");
        var games = new SqliteGameRepository(f.Db);
        var sessions = new SqliteSessionRepository(f.Db);
        var annotations = new SqliteSessionAnnotationRepository(f.Db);

        async Task<ResolvedTriState> ResolveAsync(TriStateKind kind) => TriStateResolution.Resolve(
            kind,
            (await sessions.FindByIdAsync(sessionId, Ct).ConfigureAwait(false))!,
            await annotations.FindAsync(sessionId, Ct).ConfigureAwait(false),
            (await games.FindByIdAsync(gameId, Ct).ConfigureAwait(false))!);

        (await ResolveAsync(TriStateKind.RayTracing)).Should().Be(new ResolvedTriState(Tri.Yes, TriStateSource.Measured));
        (await ResolveAsync(TriStateKind.PathTracing)).Should().Be(ResolvedTriState.NotApplicable);

        await annotations.UpsertAsync(new SessionAnnotation { SessionId = sessionId, RtOverride = Tri.No }, Ct);
        await games.SetTriStateDefaultAsync(gameId, TriStateKind.PathTracing, Tri.Yes, Ct);
        (await ResolveAsync(TriStateKind.RayTracing)).Should().Be(new ResolvedTriState(Tri.No, TriStateSource.Manual));
        (await ResolveAsync(TriStateKind.PathTracing)).Should().Be(new ResolvedTriState(Tri.Yes, TriStateSource.Inherited), "the game default, because the session measured nothing");

        await annotations.UpsertAsync(new SessionAnnotation { SessionId = sessionId }, Ct);
        (await ResolveAsync(TriStateKind.RayTracing)).Should().Be(new ResolvedTriState(Tri.Yes, TriStateSource.Measured), "the measurement was beside the override, not under it");
    }
}
