using System.Globalization;
using Dapper;
using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Sessions;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.Infrastructure.Tests.Persistence;

/// <summary>
/// Two entries for one executable under two drive letters become one (2026-09-23, D26), on a real ledger: the sessions
/// re-parented before the cascade could take them, the path taken after the UNIQUE holder is gone, a block either carried
/// kept, no consent moved — or nothing written at all when a row is not what the plan saw.
/// </summary>
public sealed class SqliteGameMergeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly ExecutableFingerprint _d = new() { ExePath = @"D:\Games\T\t.exe", SizeBytes = 10, MtimeUnixMs = 20 };

    private static readonly ExecutableFingerprint _h = _d with { ExePath = @"H:\Games\T\t.exe" };

    private static Task<int> SetAsync(LedgerFixture f, long id, string assignments) =>
        f.Db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition($"UPDATE games SET {assignments} WHERE id = @id", new { id }, tx, cancellationToken: ct)), Ct).AsTask();

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

    private static async Task<(SqliteGameRepository Repo, GameRow Stale, GameRow Twin)> TwinsAsync(LedgerFixture f, long staleAdded, long twinAdded)
    {
        var repo = new SqliteGameRepository(f.Db);
        GameRow stale = await repo.EnsureAsync(_d, "Title", Ct).ConfigureAwait(false);
        GameRow twin = await repo.EnsureAsync(_h, "Title (H:)", Ct).ConfigureAwait(false);
        await SetAsync(f, stale.Id, "added_at = " + staleAdded.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        await SetAsync(f, twin.Id, "added_at = " + twinAdded.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        return (repo, stale, twin);
    }

    private static async Task<GameMergePlan> PlanAsync(SqliteGameRepository repo, GameRow stale, GameRow twin) =>
        GameMergePlan.For((await repo.FindByIdAsync(stale.Id, Ct).ConfigureAwait(false))!, (await repo.FindByIdAsync(twin.Id, Ct).ConfigureAwait(false))!, _h);

    [Fact]
    public async Task TheEntryMadeFirstTakesTheTwinsSessionsItsBlockAndItsPath()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        (SqliteGameRepository repo, GameRow stale, GameRow twin) = await TwinsAsync(f, staleAdded: 1_000, twinAdded: 2_000);
        await SetAsync(f, stale.Id, "hook_enabled = 1, hook_consent_at = 5, hook_consent_provenance = 'ConsentDialog', hook_consent_disclosure_version = 'v', "
                                    + "hook_prescan_state = 'clean', hook_crash_count = 1, hook_last_injected_at = 30");
        await SetAsync(f, twin.Id, "hook_blocked_reason = 'BlockedModule: Easy Anti-Cheat', hook_prescan_state = 'blocked', hook_crash_count = 2, hook_last_injected_at = 40");
        long mine = await AddSessionAsync(f, stale.Id);
        long theirs = await AddSessionAsync(f, twin.Id);

        int? moved = await new SqliteGameMerge(f.Db).MergeAsync(await PlanAsync(repo, stale, twin), Ct);

        moved.Should().Be(1);
        (await repo.FindByIdAsync(twin.Id, Ct)).Should().BeNull("the twin is gone");
        GameRow kept = (await repo.FindByIdAsync(stale.Id, Ct))!;
        kept.Fingerprint.Should().Be(_h, "it lives at the file now — the same bytes, so the move keeps its consent record");
        kept.HookConsentAt.Should().NotBeNull();
        kept.HookBlockedReason.Should().Be("BlockedModule: Easy Anti-Cheat", "a block either entry carried is kept; nothing clears one");
        kept.HookEnabled.Should().BeFalse("and the block turns hooking off");
        kept.HookPrescanState.Should().Be("blocked");
        kept.HookCrashCount.Should().Be(3);
        kept.HookLastInjectedAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(40));
        kept.AddedAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1_000));
        var sessions = new SqliteSessionRepository(f.Db);
        (await sessions.FindByIdAsync(mine, Ct))!.GameId.Should().Be(stale.Id);
        (await sessions.FindByIdAsync(theirs, Ct))!.GameId.Should().Be(stale.Id, "re-parented before the delete, not cascaded away with it");
        (await repo.FindAsync(_d.ExePath, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task WhenTheTwinWasMadeFirstItStaysAndNoConsentMovesToIt()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        (SqliteGameRepository repo, GameRow stale, GameRow twin) = await TwinsAsync(f, staleAdded: 2_000, twinAdded: 1_000);
        await SetAsync(f, stale.Id, "hook_enabled = 1, hook_consent_at = 5, hook_consent_provenance = 'ConsentDialog', hook_consent_disclosure_version = 'v', hook_prescan_state = 'clean'");
        long session = await AddSessionAsync(f, stale.Id);

        (await new SqliteGameMerge(f.Db).MergeAsync(await PlanAsync(repo, stale, twin), Ct)).Should().Be(1);

        (await repo.FindByIdAsync(stale.Id, Ct)).Should().BeNull();
        GameRow kept = (await repo.FindByIdAsync(twin.Id, Ct))!;
        kept.Fingerprint.Should().Be(_h);
        kept.HookEnabled.Should().BeFalse("the dropped entry's consent is about that entry; a merge never grants");
        kept.HookConsentAt.Should().BeNull();
        (await new SqliteSessionRepository(f.Db).FindByIdAsync(session, Ct))!.GameId.Should().Be(twin.Id);
    }

    [Fact]
    public async Task ACrashAutoDisableStillInForceIsCarriedAndOneAlreadyLiftedIsNot()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        (SqliteGameRepository repo, GameRow stale, GameRow twin) = await TwinsAsync(f, staleAdded: 1_000, twinAdded: 2_000);
        await SetAsync(f, stale.Id, "hook_enabled = 1");
        await SetAsync(f, twin.Id, "hook_enabled = 0, hook_autodisabled_reason = 'crashed 3 times', hook_autodisabled_at = 77");

        (await new SqliteGameMerge(f.Db).MergeAsync(await PlanAsync(repo, stale, twin), Ct)).Should().Be(0);

        GameRow kept = (await repo.FindByIdAsync(stale.Id, Ct))!;
        kept.HookEnabled.Should().BeFalse("the crash policy turned this executable's hooking off, under either entry");
        kept.HookAutoDisabledReason.Should().Be("crashed 3 times");

        await using LedgerFixture g = await LedgerFixture.OpenAsync();
        (repo, stale, twin) = await TwinsAsync(g, staleAdded: 1_000, twinAdded: 2_000);
        await SetAsync(g, stale.Id, "hook_enabled = 1");
        await SetAsync(g, twin.Id, "hook_enabled = 1, hook_autodisabled_reason = 'crashed 3 times', hook_autodisabled_at = 77");

        (await new SqliteGameMerge(g.Db).MergeAsync(await PlanAsync(repo, stale, twin), Ct)).Should().Be(0);

        GameRow lifted = (await repo.FindByIdAsync(stale.Id, Ct))!;
        lifted.HookEnabled.Should().BeTrue("the user turned hooking back on after that auto-disable; it is history, not a state");
        lifted.HookAutoDisabledReason.Should().BeNull();
    }

    [Fact]
    public async Task ARowThatIsNotWhatThePlanSawIsNotMergedAndNothingIsWritten()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        (SqliteGameRepository repo, GameRow stale, GameRow twin) = await TwinsAsync(f, staleAdded: 1_000, twinAdded: 2_000);
        long session = await AddSessionAsync(f, twin.Id);
        GameMergePlan plan = await PlanAsync(repo, stale, twin);
        var merge = new SqliteGameMerge(f.Db);

        // The survivor's bytes are no longer the file's: a move now would carry its consent to another binary.
        await SetAsync(f, stale.Id, "exe_size_bytes = 11");
        (await merge.MergeAsync(plan, Ct)).Should().BeNull();

        // The twin was removed from the library meanwhile.
        await SetAsync(f, stale.Id, "exe_size_bytes = 10");
        (await repo.RemoveAsync(twin.Id, keepSessions: true, Ct)).Should().BeTrue();
        (await merge.MergeAsync(plan, Ct)).Should().BeNull();

        (await repo.FindByIdAsync(stale.Id, Ct))!.Fingerprint.Should().Be(_d, "nothing moved");
        (await repo.FindByIdAsync(twin.Id, Ct)).Should().NotBeNull("nothing deleted");
        (await new SqliteSessionRepository(f.Db).FindByIdAsync(session, Ct))!.GameId.Should().Be(twin.Id, "nothing re-parented");
    }
}
