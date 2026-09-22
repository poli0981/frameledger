using System.Data.Common;
using Dapper;
using FrameLedger.Application.Persistence;
using Microsoft.Data.Sqlite;

namespace FrameLedger.Infrastructure.Persistence;

/// <summary>
/// <see cref="IGameMerge"/> over the ledger (2026-09-23, HANDOFF D26): two entries for one executable under two drive
/// letters become one, in one transaction, or nothing is written.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order is the schema's.</b> <c>sessions.game_id</c> is the only reference to <c>games</c> (0001–0008) and it
/// cascades on delete, so the sessions are re-parented BEFORE the dropped row is deleted; <c>exe_path</c> is UNIQUE, so a
/// survivor that moves takes the path AFTER the row that held it is gone.
/// </para>
/// <para>
/// <b>Only ever toward hooking off.</b> A block the dropped entry carried is kept (it is never cleared anywhere, and a
/// merge must not be the way one disappears); a crash auto-disable it is still under turns the survivor's hooking off
/// too; its crash count is added. Nothing about consent is read from the dropped entry or written to the survivor —
/// <c>hook_enabled</c> only ever goes to 0 here, and the survivor's own consent is untouched: a survivor that moves has
/// the target's bytes (the plan's condition, checked again below), which is the move <see cref="IGameRepository.RelocateExecutableAsync"/>
/// makes with consent kept. This class is therefore a writer of <c>hook_blocked_reason</c> that can only copy a block,
/// never set one from nothing or clear one (<c>06_DATA_MODEL</c> §Writer ownership).
/// </para>
/// </remarks>
public sealed class SqliteGameMerge : IGameMerge
{
    private const string _read =
        "SELECT id, exe_path, exe_size_bytes, exe_mtime_ms, removed_at, hook_enabled, hook_blocked_reason, hook_autodisabled_reason, "
        + "hook_autodisabled_at, hook_crash_count, hook_last_injected_at FROM games WHERE id = @id";

    private const string _reparent = "UPDATE sessions SET game_id = @keep WHERE game_id = @drop";

    private const string _carry =
        "UPDATE games SET hook_blocked_reason = @blocked, hook_autodisabled_reason = @autoReason, hook_autodisabled_at = @autoAt, "
        + "hook_crash_count = @crashes, hook_last_injected_at = @injected, "
        + "hook_enabled = CASE WHEN @hookOff THEN 0 ELSE hook_enabled END, "
        + "hook_prescan_state = CASE WHEN @blockCarried THEN 'blocked' ELSE hook_prescan_state END, "
        + "updated_at = @now WHERE id = @keep";

    private const string _delete = "DELETE FROM games WHERE id = @drop";

    private const string _move = "UPDATE games SET exe_path = @path, updated_at = @now WHERE id = @keep";

    private readonly LedgerDatabase _db;

    public SqliteGameMerge(LedgerDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    public ValueTask<int?> MergeAsync(GameMergePlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return _db.WriteAsync(async (c, tx, token) =>
        {
            Twin? keep = await ReadAsync(c, tx, plan.SurvivorId, token).ConfigureAwait(false);
            Twin? drop = await ReadAsync(c, tx, plan.DroppedId, token).ConfigureAwait(false);
            if (!AsPlanned(plan, keep, drop))
            {
                return (int?)null;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            int sessions = await c.ExecuteAsync(new CommandDefinition(_reparent, new { keep = plan.SurvivorId, drop = plan.DroppedId }, tx, cancellationToken: token))
                .ConfigureAwait(false);

            // A crash auto-disable the dropped entry is still under (hooking off because of it) turns the survivor's hooking off
            // too; one it was re-enabled after is history, and the survivor keeps its own.
            bool autoCarried = keep!.AutoDisabledReason is null && drop!.AutoDisabledReason is not null && !drop.HookEnabled;
            bool blockCarried = keep.BlockedReason is null && drop!.BlockedReason is not null;
            var carry = new
            {
                keep = plan.SurvivorId,
                blocked = keep.BlockedReason ?? drop!.BlockedReason,
                autoReason = autoCarried ? drop!.AutoDisabledReason : keep.AutoDisabledReason,
                autoAt = autoCarried ? drop!.AutoDisabledAt : keep.AutoDisabledAt,
                crashes = keep.CrashCount + drop!.CrashCount,
                injected = Later(keep.LastInjectedAt, drop.LastInjectedAt),
                hookOff = blockCarried || autoCarried,
                blockCarried,
                now,
            };
            await c.ExecuteAsync(new CommandDefinition(_carry, carry, tx, cancellationToken: token)).ConfigureAwait(false);
            await c.ExecuteAsync(new CommandDefinition(_delete, new { drop = plan.DroppedId }, tx, cancellationToken: token)).ConfigureAwait(false);
            if (plan.SurvivorMoves)
            {
                await c.ExecuteAsync(new CommandDefinition(_move, new { keep = plan.SurvivorId, path = plan.Target.ExePath, now }, tx, cancellationToken: token))
                    .ConfigureAwait(false);
            }

            return (int?)sessions;
        }, ct);
    }

    /// <summary>
    /// Both rows are still what the plan saw: there, in the library, at the paths it read — and a survivor that moves still
    /// holds the target's bytes, so the move keeps its consent for the file it was given for.
    /// </summary>
    private static bool AsPlanned(GameMergePlan plan, Twin? keep, Twin? drop) =>
        keep is { RemovedAt: null } && drop is { RemovedAt: null }
        && string.Equals(keep.ExePath, plan.SurvivorPath, StringComparison.OrdinalIgnoreCase)
        && string.Equals(drop.ExePath, plan.DroppedPath, StringComparison.OrdinalIgnoreCase)
        && (plan.SurvivorMoves
            ? string.Equals(drop.ExePath, plan.Target.ExePath, StringComparison.OrdinalIgnoreCase)
              && keep.SizeBytes == plan.Target.SizeBytes && keep.MtimeMs == plan.Target.MtimeUnixMs
            : string.Equals(keep.ExePath, plan.Target.ExePath, StringComparison.OrdinalIgnoreCase));

    private static long? Later(long? a, long? b) => a is null ? b : b is null ? a : Math.Max(a.Value, b.Value);

    private static Task<Twin?> ReadAsync(SqliteConnection c, SqliteTransaction tx, long id, CancellationToken ct) =>
        SqliteReaders.ReadOneAsync(c, new CommandDefinition(_read, new { id }, tx, cancellationToken: ct), Read);

    private static Twin Read(DbDataReader r) => new(
        r.GetInt64(0),
        r.GetString(1),
        SqliteReaders.Int64(r, 2),
        SqliteReaders.Int64(r, 3),
        SqliteReaders.Int64(r, 4),
        r.GetInt64(5) != 0,
        SqliteReaders.String(r, 6),
        SqliteReaders.String(r, 7),
        SqliteReaders.Int64(r, 8),
        r.GetInt32(9),
        SqliteReaders.Int64(r, 10));

    private sealed record Twin(
        long Id,
        string ExePath,
        long? SizeBytes,
        long? MtimeMs,
        long? RemovedAt,
        bool HookEnabled,
        string? BlockedReason,
        string? AutoDisabledReason,
        long? AutoDisabledAt,
        int CrashCount,
        long? LastInjectedAt);
}
