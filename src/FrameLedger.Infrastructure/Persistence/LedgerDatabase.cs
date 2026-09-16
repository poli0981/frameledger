using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace FrameLedger.Infrastructure.Persistence;

/// <summary>
/// The one open connection to <c>ledger.db</c> in this process, with <c>06_DATA_MODEL</c>'s pragmas set
/// and its schema migrated before anyone reads it.
/// </summary>
/// <remarks>
/// <para>
/// <b>One connection, one gate.</b> <c>04_CAPTURE</c> §Threading model: the session loop finalizes on its
/// own task, the console verbs run on the main one, and SQLite serialises writers anyway — so a single
/// connection behind a <see cref="SemaphoreSlim"/> is the honest shape, and every write is an explicit
/// transaction (<see cref="WriteAsync{T}"/>) or does not happen. WAL lets another process (the UI, P3)
/// read while this one writes.
/// </para>
/// <para>
/// <b>Opening refuses a newer schema.</b> A ledger written by a build newer than this one is not read on
/// a guess; <see cref="OpenAsync"/> throws <see cref="LedgerSchemaException"/> and the caller tells the
/// user to update.
/// </para>
/// <para>
/// <b>A read-only open migrates nothing (2026-09-16).</b> <see cref="OpenReadOnlyAsync"/> is for the verbs that
/// only print — the Agent's <c>sessions</c>, <c>db path</c>, <c>consent list</c>, <c>killswitch status</c> and the
/// App's <c>--diag</c>. Until it existed, every one of them went through <see cref="OpenAsync"/>, and a
/// "read-only" verb run against a ledger at an older schema <em>applied the missing scripts</em> to it — measured
/// on the owner's own ledger, whose on-disk file was at schema 2 while the running App and Agent saw schema 4
/// through the shared WAL index. A verb whose name says it reads must not be a writer by accident.
/// </para>
/// <para>
/// <b>The WAL is checkpointed at open and at close, and both are reported.</b> The same incident found a
/// 4 MB WAL whose 1006 frames carried a salt one generation older than the WAL header, over a main file that
/// no checkpoint had reached: a fresh connection saw nothing written that day. The mechanism is unrecorded
/// (<c>06_DATA_MODEL</c> §Migrations carries what was observed); what this class can do about it is make the
/// next occurrence visible — <see cref="OpenDiagnostics"/> says how many frames the WAL held at open and how
/// many a passive checkpoint moved, and <see cref="DisposeAsync"/> runs a truncating checkpoint so a clean
/// close leaves the main file holding everything.
/// </para>
/// </remarks>
public sealed class LedgerDatabase : IAsyncDisposable
{
    /// <summary><c>06_DATA_MODEL</c>: <c>busy_timeout=5000</c>.</summary>
    public const int DefaultBusyTimeoutMs = 5000;

    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Action<string>? _diagnostics;

    private LedgerDatabase(string path, SqliteConnection connection, MigrationOutcome migration, long schemaVersion,
        bool isReadOnly, LedgerWalCheckpoint? openCheckpoint, Action<string>? diagnostics)
    {
        Path = path;
        _connection = connection;
        Migration = migration;
        SchemaVersion = schemaVersion;
        IsReadOnly = isReadOnly;
        OpenDiagnostics = openCheckpoint;
        _diagnostics = diagnostics;
    }

    /// <summary>The file this instance opened.</summary>
    public string Path { get; }

    /// <summary>What opening did to the schema.</summary>
    public MigrationOutcome Migration { get; }

    /// <summary>The schema version after opening.</summary>
    public long SchemaVersion { get; }

    /// <summary>True when opened by <see cref="OpenReadOnlyAsync"/>: no migration ran, and <see cref="WriteAsync{T}"/> refuses.</summary>
    public bool IsReadOnly { get; }

    /// <summary>
    /// The passive checkpoint <see cref="OpenAsync"/> ran after migrating, or null for a read-only open. A WAL with
    /// frames that a passive checkpoint could not move is another process's live write set — or the incident above.
    /// </summary>
    public LedgerWalCheckpoint? OpenDiagnostics { get; }

    /// <summary>
    /// Opens (creating the file and its directory if needed), sets the pragmas, migrates. A path is
    /// REQUIRED: <see cref="LedgerPaths.DefaultDatabase"/> is the Agent's; anything else says why.
    /// <paramref name="diagnostics"/> receives one line per checkpoint (open and close) when given.
    /// </summary>
    public static async Task<LedgerDatabase> OpenAsync(string path, int busyTimeoutMs = DefaultBusyTimeoutMs,
        Action<string>? diagnostics = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegative(busyTimeoutMs);

        string full = System.IO.Path.GetFullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);

        SqliteConnection connection = Connect(full, SqliteOpenMode.ReadWriteCreate);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            // 06_DATA_MODEL's four pragmas. busy_timeout first: WAL's own journal switch can contend.
            await BusyTimeoutAsync(connection, busyTimeoutMs, ct).ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition("PRAGMA journal_mode = WAL", cancellationToken: ct)).ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition("PRAGMA synchronous = NORMAL", cancellationToken: ct)).ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition("PRAGMA foreign_keys = ON", cancellationToken: ct)).ConfigureAwait(false);

            MigrationOutcome migration = await MigrationRunner.ApplyAsync(connection, ct).ConfigureAwait(false);
            long version = await VersionAsync(connection, ct).ConfigureAwait(false);
            if (migration == MigrationOutcome.NewerThanThisBuild)
            {
                throw new LedgerSchemaException(
                    $"{full} is at schema version {version}, newer than this build's {MigrationRunner.LatestVersion}; refusing to read it");
            }

            LedgerWalCheckpoint atOpen = await CheckpointAsync(connection, "passive", ct).ConfigureAwait(false);
            diagnostics?.Invoke($"ledger: wal at open — {atOpen}");
            return new LedgerDatabase(full, connection, migration, version, isReadOnly: false, atOpen, diagnostics);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Opens an EXISTING ledger for reading only: no migration, no pragma that writes, and every statement under
    /// <c>query_only</c>. Refuses a schema newer than this build (as <see cref="OpenAsync"/> does) AND one older than
    /// it — the older file needs a read-write open to migrate, and this one will not do it on a print verb's behalf.
    /// </summary>
    /// <remarks>
    /// The connection is <see cref="SqliteOpenMode.ReadWrite"/> rather than <see cref="SqliteOpenMode.ReadOnly"/>
    /// because a WAL database needs its <c>-shm</c> index created by whoever opens it first, and SQLite's read-only
    /// mode cannot create it; <c>PRAGMA query_only</c> is what keeps this connection from changing a row, and the
    /// absence of <see cref="MigrationRunner"/> is what keeps it from changing the schema.
    /// </remarks>
    public static async Task<LedgerDatabase> OpenReadOnlyAsync(string path, int busyTimeoutMs = DefaultBusyTimeoutMs,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegative(busyTimeoutMs);

        string full = System.IO.Path.GetFullPath(path);
        if (!File.Exists(full))
        {
            throw new FileNotFoundException($"{full} does not exist; a read-only open creates nothing", full);
        }

        SqliteConnection connection = Connect(full, SqliteOpenMode.ReadWrite);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            await BusyTimeoutAsync(connection, busyTimeoutMs, ct).ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition("PRAGMA query_only = 1", cancellationToken: ct)).ConfigureAwait(false);

            long version = await VersionAsync(connection, ct).ConfigureAwait(false);
            if (version > MigrationRunner.LatestVersion)
            {
                throw new LedgerSchemaException(
                    $"{full} is at schema version {version}, newer than this build's {MigrationRunner.LatestVersion}; refusing to read it");
            }

            if (version < MigrationRunner.LatestVersion)
            {
                throw new LedgerSchemaException(
                    $"{full} is at schema version {version}, older than this build's {MigrationRunner.LatestVersion}; a read-only open migrates nothing — run the App or the Agent's --serve once to bring it forward");
            }

            return new LedgerDatabase(full, connection, MigrationOutcome.AlreadyCurrent, version, isReadOnly: true, openCheckpoint: null, diagnostics: null);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Runs <paramref name="query"/> on the connection, serialised with every other caller.</summary>
    public async ValueTask<T> ReadAsync<T>(Func<SqliteConnection, CancellationToken, Task<T>> query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await query(_connection, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Runs <paramref name="work"/> OUTSIDE any transaction, serialised with every other caller (P4 PR-7): for the
    /// statements SQLite refuses inside one — <c>VACUUM</c>, <c>VACUUM INTO</c>, <c>wal_checkpoint</c> — and only for
    /// maintenance. A domain write goes through <see cref="WriteAsync{T}"/>.
    /// </summary>
    public async ValueTask<T> MaintainAsync<T>(Func<SqliteConnection, CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        RefuseIfReadOnly();

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await work(_connection, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Runs <paramref name="work"/> inside one explicit transaction: committed when it returns, rolled back
    /// when it throws. <c>06_DATA_MODEL</c>: "all writes in explicit transactions".
    /// </summary>
    public async ValueTask<T> WriteAsync<T>(Func<SqliteConnection, SqliteTransaction, CancellationToken, Task<T>> work,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        RefuseIfReadOnly();

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var tx = (SqliteTransaction)await _connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            await using (tx.ConfigureAwait(false))
            {
                try
                {
                    T result = await work(_connection, tx, ct).ConfigureAwait(false);
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                    return result;
                }
                catch
                {
                    await tx.RollbackAsync(ct).ConfigureAwait(false);
                    throw;
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// A read-write instance checkpoints the WAL (<c>TRUNCATE</c>) before closing, so that a clean close leaves the
    /// main file complete whether or not SQLite's own close-time checkpoint runs; the result goes to the diagnostics
    /// sink. A checkpoint that cannot complete — another process holds a read transaction — is reported, not thrown.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!IsReadOnly)
        {
            try
            {
                LedgerWalCheckpoint atClose = await CheckpointAsync(_connection, "truncate", CancellationToken.None).ConfigureAwait(false);
                _diagnostics?.Invoke($"ledger: wal at close — {atClose}");
            }
            catch (SqliteException ex)
            {
                _diagnostics?.Invoke($"ledger: wal at close — checkpoint failed: {ex.Message}");
            }
        }

        await _connection.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private static SqliteConnection Connect(string full, SqliteOpenMode mode) => new(new SqliteConnectionStringBuilder
    {
        DataSource = full,
        Mode = mode,
        Pooling = false,
    }.ToString());

    private static Task<int> BusyTimeoutAsync(SqliteConnection connection, int busyTimeoutMs, CancellationToken ct) =>
        connection.ExecuteAsync(new CommandDefinition("PRAGMA busy_timeout = " + busyTimeoutMs.ToString(CultureInfo.InvariantCulture), cancellationToken: ct));

    private static async Task<long> VersionAsync(SqliteConnection connection, CancellationToken ct)
    {
        bool hasTable = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'schema_migrations'",
            cancellationToken: ct)).ConfigureAwait(false) > 0;
        return hasTable
            ? await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
                "SELECT MAX(version) FROM schema_migrations", cancellationToken: ct)).ConfigureAwait(false) ?? 0
            : 0;
    }

    private static async Task<LedgerWalCheckpoint> CheckpointAsync(SqliteConnection connection, string mode, CancellationToken ct)
    {
        (long busy, long log, long checkpointed) = await connection.QuerySingleAsync<(long, long, long)>(new CommandDefinition(
            "PRAGMA wal_checkpoint(" + mode + ")", cancellationToken: ct)).ConfigureAwait(false);
        return new LedgerWalCheckpoint(mode, Busy: busy != 0, WalFrames: log, Checkpointed: checkpointed);
    }

    private void RefuseIfReadOnly()
    {
        if (IsReadOnly)
        {
            throw new InvalidOperationException($"{Path} was opened read-only; this verb prints and does not write");
        }
    }
}
