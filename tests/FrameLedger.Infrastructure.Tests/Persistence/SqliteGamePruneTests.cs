// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using Dapper;
using FluentAssertions;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Consent;
using FrameLedger.Domain.Sessions;
using FrameLedger.Infrastructure.Persistence;

namespace FrameLedger.Infrastructure.Tests.Persistence;

/// <summary>
/// beta.19, D55: the uninstalled entry's delete on a real ledger — it goes only while, in the same statement, it is the same
/// row at the same path, in the library, with no session stored and no anti-cheat block, exception or failed-trial mark.
/// </summary>
public sealed class SqliteGamePruneTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly ExecutableFingerprint _exe = new() { ExePath = @"D:\another\it\uninstalled\Game.exe", SizeBytes = 10, MtimeUnixMs = 20 };

    private static Task<int> SetAsync(LedgerFixture f, long id, string assignments) =>
        f.Db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition($"UPDATE games SET {assignments} WHERE id = @id", new { id }, tx, cancellationToken: ct)), Ct).AsTask();

    private static async Task AddSessionAsync(LedgerFixture f, long gameId)
    {
        long snapshotId = await new SqliteHardwareSnapshotRepository(f.Db).EnsureAsync(new HardwareSnapshot { GpuName = "G" }, DateTimeOffset.UnixEpoch, Ct).ConfigureAwait(false);
        _ = await new SqliteSessionRepository(f.Db).InsertFinalizedAsync(new FinalizedSession
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
    public async Task AnUninstalledEntryWithNothingToKeepIsDeleted()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);
        GameRow row = await repo.EnsureAsync(_exe, "Uninstalled", Ct);
        await SetAsync(f, row.Id, "hook_autodisabled_reason = 'executable is x86: the hook runs in x64 processes only', hook_autodisabled_at = 5");

        (await new SqliteGamePrune(f.Db).RemoveUninstalledAsync(row.Id, @"d:\ANOTHER\it\uninstalled\game.EXE", Ct)).Should().BeTrue("the path compares as the column does, without case; an auto-disable is not a block");

        (await repo.FindByIdAsync(row.Id, Ct)).Should().BeNull();
    }

    [Theory]
    [InlineData("hook_blocked_reason = 'BlockedModule: Easy Anti-Cheat'", "a block is never cleared")]
    [InlineData("hook_prescan_state = 'blocked'", "the pre-scan's block")]
    [InlineData("ac_exception_at = 5", "an exception's grant")]
    [InlineData("ac_exception_trial_failed_at = 5", "D38's mark, for good")]
    [InlineData("removed_at = 5", "an entry already removed keeps its sessions' owner")]
    public async Task AnEntryCarryingWhatMustOutliveItIsKept(string assignments, string because)
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);
        GameRow row = await repo.EnsureAsync(_exe, "Uninstalled", Ct);
        await SetAsync(f, row.Id, assignments);

        (await new SqliteGamePrune(f.Db).RemoveUninstalledAsync(row.Id, _exe.ExePath, Ct)).Should().BeFalse(because);

        (await repo.FindByIdAsync(row.Id, Ct)).Should().NotBeNull(because);
    }

    [Fact]
    public async Task AnEntryWithASessionOrAtAnotherPathIsKept()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        var repo = new SqliteGameRepository(f.Db);
        GameRow played = await repo.EnsureAsync(_exe, "Played", Ct);
        await AddSessionAsync(f, played.Id);
        GameRow moved = await repo.EnsureAsync(_exe with { ExePath = @"D:\another\it\moved\Game.exe" }, "Moved", Ct);
        var prune = new SqliteGamePrune(f.Db);

        (await prune.RemoveUninstalledAsync(played.Id, _exe.ExePath, Ct)).Should().BeFalse("a session was recorded — the owner's condition");
        (await prune.RemoveUninstalledAsync(moved.Id, @"D:\another\it\old-place\Game.exe", Ct)).Should().BeFalse("the entry was re-pointed after the sweep read it");

        (await repo.FindByIdAsync(played.Id, Ct)).Should().NotBeNull();
        (await repo.FindByIdAsync(moved.Id, Ct)).Should().NotBeNull();
    }
}
