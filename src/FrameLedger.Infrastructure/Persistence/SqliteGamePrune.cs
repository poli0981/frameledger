// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using Dapper;
using FrameLedger.Application.Persistence;

namespace FrameLedger.Infrastructure.Persistence;

/// <summary>
/// <see cref="IGamePrune"/> over the ledger (beta.19, owner decision D55): one statement that deletes an uninstalled entry
/// only while every stored condition still holds — the same row at the same path, in the library, no session stored
/// (<c>sessions.game_id</c> is the only reference to <c>games</c> and would cascade), and no anti-cheat block, exception or
/// failed-trial mark. Whatever changed between the sweep's read and this write — a session finalized, a block written, the
/// entry re-pointed — leaves the row as it is.
/// </summary>
public sealed class SqliteGamePrune : IGamePrune
{
    private const string _delete =
        "DELETE FROM games WHERE id = @id AND exe_path = @path AND removed_at IS NULL "
        + "AND hook_blocked_reason IS NULL AND hook_prescan_state <> 'blocked' "
        + "AND ac_exception_at IS NULL AND ac_exception_trial_failed_at IS NULL "
        + "AND NOT EXISTS (SELECT 1 FROM sessions WHERE sessions.game_id = games.id)";

    private readonly LedgerDatabase _db;

    public SqliteGamePrune(LedgerDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc />
    public async ValueTask<bool> RemoveUninstalledAsync(long gameId, string exePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        int deleted = await _db.WriteAsync((c, tx, token) => c.ExecuteAsync(new CommandDefinition(_delete, new { id = gameId, path = exePath }, tx, cancellationToken: token)), ct)
            .ConfigureAwait(false);
        return deleted == 1;
    }
}
