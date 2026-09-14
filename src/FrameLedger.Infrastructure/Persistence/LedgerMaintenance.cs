using System.IO;
using Dapper;

namespace FrameLedger.Infrastructure.Persistence;

/// <summary>
/// Tools ▸ Database maintenance's App-side half (P4 PR-7, <c>06_DATA_MODEL</c> §Retention: "which also offers
/// <c>PRAGMA integrity_check</c>, <c>VACUUM</c>, backup"). None of it changes a row: the integrity check and the backup
/// only read, and <c>VACUUM</c> rewrites the file's pages with the same contents. Removing raw series is the Agent's
/// (<c>SweepRetention</c>), because the blob tables are its (§Writer ownership).
/// </summary>
public sealed class LedgerMaintenance
{
    private readonly LedgerDatabase _db;

    public LedgerMaintenance(LedgerDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <summary><c>PRAGMA integrity_check</c>: the single line <c>ok</c> when the file is sound, otherwise SQLite's own problem lines (at most 100).</summary>
    public ValueTask<IReadOnlyList<string>> CheckIntegrityAsync(CancellationToken ct = default) =>
        _db.ReadAsync<IReadOnlyList<string>>(async (c, token) =>
        {
            IEnumerable<string> lines = await c.QueryAsync<string>(new CommandDefinition("PRAGMA integrity_check", cancellationToken: token)).ConfigureAwait(false);
            return [.. lines];
        }, ct);

    /// <summary>True when <see cref="CheckIntegrityAsync"/>'s answer is SQLite's healthy one.</summary>
    public static bool IsHealthy(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return lines.Count == 1 && string.Equals(lines[0], "ok", StringComparison.Ordinal);
    }

    /// <summary>
    /// A consistent, compacted copy of the ledger at <paramref name="destination"/> through <c>VACUUM INTO</c>: other
    /// connections — the Agent's — keep reading and writing while it runs, and the copy is a complete database, not a
    /// file copy caught between a write and its WAL. The destination must not exist; the caller asked the user first.
    /// </summary>
    /// <exception cref="IOException">The destination exists.</exception>
    public async ValueTask BackupAsync(string destination, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        string full = Path.GetFullPath(destination);
        string ledger = Path.GetFullPath(_db.Path);
        if (string.Equals(full, ledger, StringComparison.OrdinalIgnoreCase) || full.StartsWith(ledger + "-", StringComparison.OrdinalIgnoreCase))
        {
            // The live ledger, its WAL or its shared-memory file: a caller that deleted "the existing file" first would
            // have deleted the database, so the destination is refused whatever the caller did before.
            throw new IOException($"{full} is the ledger itself; a backup goes to another file");
        }

        if (File.Exists(full))
        {
            throw new IOException($"{full} exists; a backup never overwrites a file");
        }

        await _db.MaintainAsync(async (c, token) =>
        {
            await c.ExecuteAsync(new CommandDefinition("VACUUM INTO @full", new { full }, cancellationToken: token)).ConfigureAwait(false);
            return true;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>VACUUM</c>, then <c>wal_checkpoint(TRUNCATE)</c> so the space is returned to the disk rather than parked in
    /// the WAL. The caller refuses while a session runs; a writer that arrives anyway makes this fail with SQLite's
    /// busy error after the busy timeout, and nothing is lost either way.
    /// </summary>
    public async ValueTask<LedgerCompaction> CompactAsync(CancellationToken ct = default)
    {
        long before = FileBytes(_db.Path);
        await _db.MaintainAsync(async (c, token) =>
        {
            await c.ExecuteAsync(new CommandDefinition("VACUUM", cancellationToken: token)).ConfigureAwait(false);
            await c.ExecuteAsync(new CommandDefinition("PRAGMA wal_checkpoint(TRUNCATE)", cancellationToken: token)).ConfigureAwait(false);
            return true;
        }, ct).ConfigureAwait(false);
        return new LedgerCompaction(before, FileBytes(_db.Path));
    }

    private static long FileBytes(string database)
    {
        long bytes = 0;
        foreach (string path in new[] { database, database + "-wal" })
        {
            var info = new FileInfo(path);
            if (info.Exists)
            {
                bytes += info.Length;
            }
        }

        return bytes;
    }
}
