using System.Text.RegularExpressions;
using Dapper;
using FluentAssertions;
using FluentAssertions.Specialized;
using FrameLedger.Infrastructure.Import;
using FrameLedger.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace FrameLedger.Infrastructure.Tests.Persistence;

/// <summary>
/// <c>14_TESTING</c>: "SQLite migrations apply cleanly from an empty file to the current schema, and re-applying
/// is a no-op" — plus the pragmas, the refusal of a newer schema, and the transaction guarantee.
/// </summary>
public sealed class LedgerDatabaseTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string[] _tables =
    [
        "schema_migrations", "games", "hardware_snapshots", "sessions", "session_segments", "frame_blobs",
        "sensor_blobs", "session_annotations", "settings", "legal_acceptance",
    ];

    [Fact]
    public async Task AnEmptyFileMigratesToTheCurrentSchema()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();

        f.Db.Migration.Should().Be(MigrationOutcome.Applied);
        f.Db.SchemaVersion.Should().Be(MigrationRunner.LatestVersion);
        IReadOnlyList<string> tables = await f.Db.ReadAsync(async (c, ct) =>
            (IReadOnlyList<string>)[.. await c.QueryAsync<string>(new CommandDefinition(
                "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name", cancellationToken: ct)).ConfigureAwait(false)], Ct);
        tables.Should().Contain(_tables);
    }

    /// <summary>Schema 0003 (2026-09-14): <c>sessions.fg_refusal</c>, the reason an identified frame generation has no factor.</summary>
    [Fact]
    public async Task ScriptThreeAddsTheFgRefusalColumn()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();

        MigrationRunner.LatestVersion.Should().BeGreaterThanOrEqualTo(3);
        IReadOnlyList<string> columns = await f.Db.ReadAsync(async (c, ct) =>
            (IReadOnlyList<string>)[.. await c.QueryAsync<string>(new CommandDefinition(
                "SELECT name FROM pragma_table_info('sessions')", cancellationToken: ct)).ConfigureAwait(false)], Ct);
        columns.Should().Contain("fg_refusal").And.Contain("fg_none_withheld_reason", "0003 appends beside 0001's columns, it edits nothing");
    }

    /// <summary>Schema 0004 (P4 PR-1): the detection cache key's exe half on <c>games</c>, apart from the consent fingerprint.</summary>
    [Fact]
    public async Task ScriptFourAddsTheDetectionCacheColumns()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();

        MigrationRunner.LatestVersion.Should().BeGreaterThanOrEqualTo(4);
        IReadOnlyList<string> columns = await f.Db.ReadAsync(async (c, ct) =>
            (IReadOnlyList<string>)[.. await c.QueryAsync<string>(new CommandDefinition(
                "SELECT name FROM pragma_table_info('games')", cancellationToken: ct)).ConfigureAwait(false)], Ct);
        columns.Should().Contain("detection_exe_size_bytes").And.Contain("detection_exe_mtime_ms")
            .And.Contain("exe_size_bytes", "the consent fingerprint keeps its own columns; the sweep never writes them");
    }

    /// <summary>Schema 0005 (2026-09-16): <c>sessions.fg_refusal_detail</c>, the numbers behind the refusal.</summary>
    [Fact]
    public async Task ScriptFiveAddsTheFgRefusalDetailColumn()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();

        MigrationRunner.LatestVersion.Should().BeGreaterThanOrEqualTo(5);
        IReadOnlyList<string> columns = await f.Db.ReadAsync(async (c, ct) =>
            (IReadOnlyList<string>)[.. await c.QueryAsync<string>(new CommandDefinition(
                "SELECT name FROM pragma_table_info('sessions')", cancellationToken: ct)).ConfigureAwait(false)], Ct);
        columns.Should().Contain("fg_refusal_detail").And.Contain("fg_refusal", "0005 appends beside 0003's column, it edits nothing");
    }

    /// <summary>Schema 0006 (2026-09-17): which part of the session <c>fg_factor</c> describes, and how much of it.</summary>
    [Fact]
    public async Task ScriptSixAddsTheFgScopeColumns()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();

        MigrationRunner.LatestVersion.Should().BeGreaterThanOrEqualTo(6);
        IReadOnlyList<string> columns = await f.Db.ReadAsync(async (c, ct) =>
            (IReadOnlyList<string>)[.. await c.QueryAsync<string>(new CommandDefinition(
                "SELECT name FROM pragma_table_info('sessions')", cancellationToken: ct)).ConfigureAwait(false)], Ct);
        columns.Should().Contain("fg_factor_scope").And.Contain("fg_steady_share").And.Contain("fg_refusal_detail", "0006 appends beside 0005's column, it edits nothing");
    }

    /// <summary>
    /// Schema 0008 (2026-09-22): the bypass is withdrawn and its five columns go with it — the one DROP COLUMN in the
    /// migration set, which is why the engine's version floor is asserted beside it (SQLite ≥ 3.35.0).
    /// </summary>
    [Fact]
    public async Task ScriptEightDropsTheBypassColumns()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();

        MigrationRunner.LatestVersion.Should().BeGreaterThanOrEqualTo(8);
        string? version = await f.Db.ReadAsync((c, ct) => c.ExecuteScalarAsync<string>(new CommandDefinition("SELECT sqlite_version()", cancellationToken: ct)), Ct);
        Version.Parse(version ?? string.Empty).Should().BeGreaterThanOrEqualTo(new Version(3, 35, 0), "ALTER TABLE ... DROP COLUMN needs it");
        IReadOnlyList<string> games = await f.Db.ReadAsync(async (c, ct) =>
            (IReadOnlyList<string>)[.. await c.QueryAsync<string>(new CommandDefinition(
                "SELECT name FROM pragma_table_info('games')", cancellationToken: ct)).ConfigureAwait(false)], Ct);
        IReadOnlyList<string> sessions = await f.Db.ReadAsync(async (c, ct) =>
            (IReadOnlyList<string>)[.. await c.QueryAsync<string>(new CommandDefinition(
                "SELECT name FROM pragma_table_info('sessions')", cancellationToken: ct)).ConfigureAwait(false)], Ct).ConfigureAwait(true);
        games.Should().NotContain("guard_bypass_at").And.NotContain("guard_bypass_disclosure_version")
            .And.Contain("hook_blocked_reason", "the block is what a finding writes now");
        sessions.Should().NotContain("guard_bypassed").And.NotContain("guard_bypass_family").And.NotContain("guard_bypass_signal");
    }

    /// <summary>
    /// Take a migrated ledger back to <paramref name="version"/>: every column a later script ADDED is dropped, newest first,
    /// and those scripts' <c>schema_migrations</c> rows go. The runner applies what is above MAX(version), so a rewind that
    /// left a later script's columns in place would fail on its ALTER the moment that script re-ran — which is what the
    /// hand-written rewind this replaced (2026-09-25) would have done as soon as schema 0010 existed.
    /// </summary>
    private static async Task RewindToAsync(SqliteConnection c, int version)
    {
        foreach (string sql in MigrationRunner.Scripts().Where(p => p.Key > version).OrderByDescending(static p => p.Key).Select(static p => p.Value))
        {
            foreach (Match m in Regex.Matches(sql, @"ALTER\s+TABLE\s+(?<table>\w+)\s+ADD\s+COLUMN\s+(?<column>\w+)", RegexOptions.IgnoreCase,
                         TimeSpan.FromSeconds(1)).Reverse())
            {
                await c.ExecuteAsync(new CommandDefinition($"ALTER TABLE {m.Groups["table"].Value} DROP COLUMN {m.Groups["column"].Value}",
                    cancellationToken: Ct)).ConfigureAwait(false);
            }
        }

        await c.ExecuteAsync(new CommandDefinition($"DELETE FROM schema_migrations WHERE version > {version}; PRAGMA wal_checkpoint(TRUNCATE);",
            cancellationToken: Ct)).ConfigureAwait(false);
    }

    /// <summary>Schema 0012 (2026-09-25): <c>sessions.driver_profile</c>, NULL for every row written before.</summary>
    [Fact]
    public async Task ScriptTwelveAddsTheDriverProfileColumn()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        MigrationRunner.LatestVersion.Should().BeGreaterThanOrEqualTo(12);
        string path = f.Path;
        await f.Db.DisposeAsync().ConfigureAwait(true);
        var c11 = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        await using (c11.ConfigureAwait(true))
        {
            await c11.OpenAsync(Ct).ConfigureAwait(true);
            await RewindToAsync(c11, 11).ConfigureAwait(true);
        }

        LedgerDatabase migrated = await LedgerDatabase.OpenAsync(path, ct: Ct).ConfigureAwait(true);
        await using (migrated.ConfigureAwait(true))
        {
            migrated.SchemaVersion.Should().Be(MigrationRunner.LatestVersion);
            long columns = await migrated.ReadAsync((c, ct) => c.ExecuteScalarAsync<long>(new CommandDefinition(
                "SELECT COUNT(*) FROM pragma_table_info('sessions') WHERE name = 'driver_profile'", cancellationToken: ct)), Ct).ConfigureAwait(true);
            columns.Should().Be(1);
        }
    }

    /// <summary>
    /// Schema 0011 (2026-09-25): what a game's files say about themselves — what the executable runs as, its two PE versions,
    /// the capability files it ships. Applied from schema 10 to a ledger holding a row: the four columns exist and read NULL,
    /// and a NULL <c>exe_machine</c> is what makes the detection sweep read the row once more.
    /// </summary>
    [Fact]
    public async Task ScriptElevenAddsTheExecutableFactsEmptyForExistingRows()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        MigrationRunner.LatestVersion.Should().BeGreaterThanOrEqualTo(11);
        string path = f.Path;
        await f.Db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition(
            "INSERT INTO games (name, exe_path, added_at, updated_at) VALUES ('A game', 'D:\\g\\game.exe', 1, 1)",
            transaction: tx, cancellationToken: ct)), Ct).ConfigureAwait(true);
        await f.Db.DisposeAsync().ConfigureAwait(true);
        var c10 = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        await using (c10.ConfigureAwait(true))
        {
            await c10.OpenAsync(Ct).ConfigureAwait(true);
            await RewindToAsync(c10, 10).ConfigureAwait(true);
        }

        LedgerDatabase migrated = await LedgerDatabase.OpenAsync(path, ct: Ct).ConfigureAwait(true);
        await using (migrated.ConfigureAwait(true))
        {
            migrated.SchemaVersion.Should().Be(MigrationRunner.LatestVersion);
            (string? Machine, string? File, string? Product, string? Libraries) facts = await migrated.ReadAsync((c, ct) =>
                c.QuerySingleAsync<(string?, string?, string?, string?)>(new CommandDefinition(
                    "SELECT exe_machine, exe_file_version, exe_product_version, library_versions FROM games", cancellationToken: ct)), Ct).ConfigureAwait(true);
            facts.Should().Be(((string?)null, (string?)null, (string?)null, (string?)null));
        }
    }

    /// <summary>
    /// Schema 0010 (2026-09-25): the Agent's pre-scan of the library keeps its own key — the rules version and the
    /// executable's size and mtime it last scanned under. Applied from schema 9 to a ledger holding a row: the three
    /// columns exist and read NULL for it, which the sweep reads as "never scanned".
    /// </summary>
    [Fact]
    public async Task ScriptTenAddsThePreScanKeyEmptyForExistingRows()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        MigrationRunner.LatestVersion.Should().BeGreaterThanOrEqualTo(10);
        string path = f.Path;
        await f.Db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition(
            "INSERT INTO games (name, exe_path, added_at, updated_at) VALUES ('A game', 'D:\\g\\game.exe', 1, 1)",
            transaction: tx, cancellationToken: ct)), Ct).ConfigureAwait(true);
        await f.Db.DisposeAsync().ConfigureAwait(true);
        var c9 = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        await using (c9.ConfigureAwait(true))
        {
            await c9.OpenAsync(Ct).ConfigureAwait(true);
            await RewindToAsync(c9, 9).ConfigureAwait(true);
        }

        LedgerDatabase migrated = await LedgerDatabase.OpenAsync(path, ct: Ct).ConfigureAwait(true);
        await using (migrated.ConfigureAwait(true))
        {
            migrated.SchemaVersion.Should().Be(MigrationRunner.LatestVersion);
            (string? Rules, long? Size, long? Mtime) key = await migrated.ReadAsync((c, ct) => c.QuerySingleAsync<(string?, long?, long?)>(
                new CommandDefinition("SELECT hook_prescan_rules_version, hook_prescan_exe_size_bytes, hook_prescan_exe_mtime_ms FROM games",
                    cancellationToken: ct)), Ct).ConfigureAwait(true);
            key.Should().Be(((string?)null, (long?)null, (long?)null));
        }
    }

    /// <summary>
    /// Schema 0009 (2026-09-23): the recording switch — on for every entry, and off once for the entries an old import made
    /// for Steam's own tools (the owner's Borderless Gaming, recorded at every boot since it starts with Windows). Applied
    /// here from schema 8 to a ledger holding a tool, a game, and a hand-added row that only shares the tool's id.
    /// </summary>
    [Fact]
    public async Task ScriptNineAddsTheRecordingSwitchOffForSteamToolsOnly()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        MigrationRunner.LatestVersion.Should().BeGreaterThanOrEqualTo(9);
        string path = f.Path;
        await f.Db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition(
            "INSERT INTO games (name, exe_path, platform, store_id, added_at, updated_at) VALUES "
            + "('Borderless Gaming', 'D:\\bg\\BorderlessGaming.exe', 'steam', '388080', 1, 1), "
            + "('A game', 'D:\\g\\game.exe', 'steam', '1091500', 1, 1), "
            + "('Hand-added', 'D:\\h\\h.exe', 'none', '388080', 1, 1)", transaction: tx, cancellationToken: ct)), Ct).ConfigureAwait(true);
        await f.Db.DisposeAsync().ConfigureAwait(true);
        var c8 = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        await using (c8.ConfigureAwait(true))
        {
            await c8.OpenAsync(Ct).ConfigureAwait(true);
            await RewindToAsync(c8, 8).ConfigureAwait(true);
        }

        LedgerDatabase migrated = await LedgerDatabase.OpenAsync(path, ct: Ct).ConfigureAwait(true);
        await using (migrated.ConfigureAwait(true))
        {
            migrated.SchemaVersion.Should().Be(MigrationRunner.LatestVersion);
            IReadOnlyList<(string Name, long Record)> rows = await migrated.ReadAsync(async (c, ct) =>
                (IReadOnlyList<(string, long)>)[.. await c.QueryAsync<(string, long)>(new CommandDefinition(
                    "SELECT name, record_sessions FROM games ORDER BY name", cancellationToken: ct)).ConfigureAwait(false)], Ct).ConfigureAwait(true);
            rows.Should().Equal(("A game", 1L), ("Borderless Gaming", 0L), ("Hand-added", 1L));
        }
    }

    /// <summary>The ids script 0009 switches off are Steam tools the import already skips (<see cref="SteamLibrarySource.KnownTools"/>), never a guess of its own.</summary>
    [Fact]
    public void ScriptNineSwitchesOffOnlyKnownSteamTools()
    {
        using Stream script = typeof(MigrationRunner).Assembly.GetManifestResourceStream("FrameLedger.Infrastructure.Persistence.Migrations.0009_record_sessions.sql")!;
        using var reader = new StreamReader(script);
        string text = reader.ReadToEnd();
        string list = text[text.IndexOf("store_id IN", StringComparison.Ordinal)..];
        List<string> ids = [.. System.Text.RegularExpressions.Regex.Matches(list, @"'(?<id>\d+)'", System.Text.RegularExpressions.RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1))
            .Select(static m => m.Groups["id"].Value)];

        ids.Should().NotBeEmpty().And.OnlyHaveUniqueItems().And.Contain("388080", "Borderless Gaming, the owner's case");
        ids.Should().BeSubsetOf(SteamLibrarySource.KnownTools);
    }

    [Fact]
    public async Task ReopeningIsANoOp()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        await using LedgerDatabase again = await f.OpenAnotherAsync(Ct);

        again.Migration.Should().Be(MigrationOutcome.AlreadyCurrent);
        again.SchemaVersion.Should().Be(f.Db.SchemaVersion);
        long rows = await again.ReadAsync((c, ct) => c.ExecuteScalarAsync<long>(new CommandDefinition("SELECT COUNT(*) FROM schema_migrations", cancellationToken: ct)), Ct);
        rows.Should().Be(MigrationRunner.LatestVersion, "one row per applied script, and nothing re-applied");
    }

    [Fact]
    public async Task ThePragmasAreTheDocumentedOnes()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync(busyTimeoutMs: 1234);

        (string journal, long sync, long fk, long busy) = await f.Db.ReadAsync(async (c, ct) => (
            (await c.ExecuteScalarAsync<string?>(new CommandDefinition("PRAGMA journal_mode", cancellationToken: ct)).ConfigureAwait(false))!,
            await c.ExecuteScalarAsync<long>(new CommandDefinition("PRAGMA synchronous", cancellationToken: ct)).ConfigureAwait(false),
            await c.ExecuteScalarAsync<long>(new CommandDefinition("PRAGMA foreign_keys", cancellationToken: ct)).ConfigureAwait(false),
            await c.ExecuteScalarAsync<long>(new CommandDefinition("PRAGMA busy_timeout", cancellationToken: ct)).ConfigureAwait(false)), Ct);

        journal.Should().Be("wal");
        sync.Should().Be(1, "NORMAL");
        fk.Should().Be(1);
        busy.Should().Be(1234);
    }

    [Fact]
    public async Task ASchemaNewerThanThisBuildIsRefusedNotGuessedAt()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        await f.Db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition(
            "INSERT INTO schema_migrations (version, applied_at) VALUES (999, 0)", transaction: tx, cancellationToken: ct)), Ct);

        Func<Task> open = async () => await f.OpenAnotherAsync(Ct).ConfigureAwait(false);

        (await open.Should().ThrowAsync<LedgerSchemaException>().ConfigureAwait(true)).Which.Message.Should().Contain("999");
    }

    [Fact]
    public async Task AWriteThatThrowsIsRolledBackWhole()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();

        Func<Task> write = async () => await f.Db.WriteAsync<int>(async (c, tx, ct) =>
        {
            await c.ExecuteAsync(new CommandDefinition("INSERT INTO settings (key, value) VALUES ('a', '1')", transaction: tx, cancellationToken: ct)).ConfigureAwait(false);
            throw new InvalidOperationException("half-way");
        }, Ct).ConfigureAwait(false);

        await write.Should().ThrowAsync<InvalidOperationException>();
        long rows = await f.Db.ReadAsync((c, ct) => c.ExecuteScalarAsync<long>(new CommandDefinition("SELECT COUNT(*) FROM settings", cancellationToken: ct)), Ct);
        rows.Should().Be(0, "the transaction rolled back, so the first statement went with the second");
    }

    [Fact]
    public async Task TwoConnectionsSerialiseThroughBusyTimeoutRatherThanFailing()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        await using LedgerDatabase other = await f.OpenAnotherAsync(Ct);

        await f.Db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition("INSERT INTO settings (key, value) VALUES ('k', 'one')", transaction: tx, cancellationToken: ct)), Ct);
        await other.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition("UPDATE settings SET value = 'two' WHERE key = 'k'", transaction: tx, cancellationToken: ct)), Ct);

        string? value = await f.Db.ReadAsync((c, ct) => c.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT value FROM settings WHERE key = 'k'", cancellationToken: ct)), Ct);
        value.Should().Be("two", "WAL lets a second opener write once the first's transaction has committed");
    }

    /// <summary>
    /// 2026-09-16: a "read-only" verb run against the owner's ledger applied two migration scripts to it, because every
    /// open went through <see cref="LedgerDatabase.OpenAsync"/>. A read-only open of an OLDER schema refuses and leaves
    /// the file byte-identical.
    /// </summary>
    [Fact]
    public async Task AReadOnlyOpenOfAnOlderSchemaRefusesAndChangesNothing()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        string path = f.Path;
        await InsertAsync(f.Db).ConfigureAwait(true);
        await f.Db.DisposeAsync().ConfigureAwait(true);
        await RewindSchemaAsync(path, 2).ConfigureAwait(true);
        byte[] before = await File.ReadAllBytesAsync(path, Ct).ConfigureAwait(true);

        Func<Task> open = async () => await LedgerDatabase.OpenReadOnlyAsync(path, ct: Ct).ConfigureAwait(false);

        ExceptionAssertions<LedgerSchemaException> refused = await open.Should().ThrowAsync<LedgerSchemaException>().ConfigureAwait(true);
        refused.WithMessage("*older than this build*");
        byte[] after = await File.ReadAllBytesAsync(path, Ct).ConfigureAwait(true);
        after.Should().Equal(before, "a read-only open migrates nothing");
        WalIsEmpty(path).Should().BeTrue("a read-only open of a checkpointed file writes no frame");
    }

    [Fact]
    public async Task AReadOnlyOpenReadsAndRefusesToWrite()
    {
        await using LedgerFixture f = await LedgerFixture.OpenAsync();
        string path = f.Path;
        await InsertAsync(f.Db).ConfigureAwait(true);
        await f.Db.DisposeAsync().ConfigureAwait(true);
        byte[] before = await File.ReadAllBytesAsync(path, Ct).ConfigureAwait(true);

        LedgerDatabase ro = await LedgerDatabase.OpenReadOnlyAsync(path, ct: Ct).ConfigureAwait(true);
        await using (ro.ConfigureAwait(true))
        {
            ro.IsReadOnly.Should().BeTrue();
            ro.Migration.Should().Be(MigrationOutcome.AlreadyCurrent);
            ro.SchemaVersion.Should().Be(MigrationRunner.LatestVersion);
            ro.OpenDiagnostics.Should().BeNull("a read-only open checkpoints nothing");
            string? value = await ro.ReadAsync((c, ct) => c.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT value FROM settings WHERE key = 'k'", cancellationToken: ct)), Ct).ConfigureAwait(true);
            value.Should().Be("v");

            Func<Task> write = async () => await ro.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition("DELETE FROM settings", transaction: tx, cancellationToken: ct)), Ct).ConfigureAwait(false);
            await write.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
            Func<Task> maintain = async () => await ro.MaintainAsync((c, ct) => c.ExecuteAsync(new CommandDefinition("VACUUM", cancellationToken: ct)), Ct).ConfigureAwait(false);
            await maintain.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
            Func<Task> raw = async () => await ro.ReadAsync((c, ct) => c.ExecuteAsync(new CommandDefinition("DELETE FROM settings", cancellationToken: ct)), Ct).ConfigureAwait(false);
            await raw.Should().ThrowAsync<SqliteException>("query_only refuses a statement that slipped past the gate").ConfigureAwait(true);
        }

        byte[] after = await File.ReadAllBytesAsync(path, Ct).ConfigureAwait(true);
        after.Should().Equal(before);
    }

    [Fact]
    public async Task AReadOnlyOpenOfAMissingFileCreatesNothing()
    {
        string dir = Path.Combine(Path.GetTempPath(), "fl-ledger-ro-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, LedgerPaths.DatabaseFileName);

        Func<Task> open = async () => await LedgerDatabase.OpenReadOnlyAsync(path, ct: Ct).ConfigureAwait(false);

        await open.Should().ThrowAsync<FileNotFoundException>().ConfigureAwait(true);
        File.Exists(path).Should().BeFalse();
        Directory.Exists(dir).Should().BeFalse("not even the directory");
    }

    /// <summary>
    /// 2026-09-16: the owner's ledger held a day's rows only in a WAL whose frames a fresh connection discarded, over
    /// a main file no checkpoint had reached. A read-write close now checkpoints explicitly and reports it, and the
    /// main file alone must hold every row afterwards.
    /// </summary>
    [Fact]
    public async Task AReadWriteCloseLeavesTheMainFileCompleteAndSaysSo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "fl-ledger-ckpt-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, LedgerPaths.DatabaseFileName);
        List<string> lines = [];
        try
        {
            LedgerDatabase db = await LedgerDatabase.OpenAsync(path, diagnostics: lines.Add, ct: Ct).ConfigureAwait(true);
            await using (db.ConfigureAwait(true))
            {
                db.OpenDiagnostics.Should().NotBeNull();
                db.OpenDiagnostics!.Mode.Should().Be("passive");
                await InsertAsync(db).ConfigureAwait(true);
            }

            lines.Should().HaveCount(2).And.SatisfyRespectively(
                open => open.Should().StartWith("ledger: wal at open — passive:"),
                close => close.Should().StartWith("ledger: wal at close — truncate:").And.NotContain("busy"));
            WalIsEmpty(path).Should().BeTrue("TRUNCATE leaves nothing in the wal");
            string? value = await ReadMainFileOnlyAsync(path).ConfigureAwait(true);
            value.Should().Be("v", "the main file alone holds the row");
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static Task<int> InsertAsync(LedgerDatabase db) =>
        db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition("INSERT INTO settings (key, value) VALUES ('k', 'v')", transaction: tx, cancellationToken: ct)), Ct).AsTask();

    private static bool WalIsEmpty(string path) => !File.Exists(path + "-wal") || new FileInfo(path + "-wal").Length == 0;

    /// <summary>Reads through a connection that ignores any wal (<c>immutable=1</c>): the main file is the only thing consulted.</summary>
    private static async Task<string?> ReadMainFileOnlyAsync(string path)
    {
        var main = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = "file:" + path.Replace('\\', '/') + "?immutable=1",
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        await using (main.ConfigureAwait(false))
        {
            await main.OpenAsync(Ct).ConfigureAwait(false);
            return await main.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT value FROM settings WHERE key = 'k'", cancellationToken: Ct)).ConfigureAwait(false);
        }
    }

    /// <summary>Makes a current ledger look like one written by an older build: the rows above <paramref name="version"/> go, the columns stay.</summary>
    private static async Task RewindSchemaAsync(string path, int version)
    {
        var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        await using (c.ConfigureAwait(false))
        {
            await c.OpenAsync(Ct).ConfigureAwait(false);
            await c.ExecuteAsync(new CommandDefinition("DELETE FROM schema_migrations WHERE version > @version", new { version }, cancellationToken: Ct)).ConfigureAwait(false);
            await c.ExecuteAsync(new CommandDefinition("PRAGMA wal_checkpoint(TRUNCATE)", cancellationToken: Ct)).ConfigureAwait(false);
        }
    }

    [Fact]
    public void TheDefaultLocationIsTheAgentsDirectory()
    {
        LedgerPaths.DefaultDatabase.Should().StartWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        LedgerPaths.DefaultDatabase.Should().EndWith(@"FrameLedger\ledger.db");
    }

    [Fact]
    public void TheScriptsAreNumberedFromOne()
    {
        SortedDictionary<int, string> scripts = MigrationRunner.Scripts();

        scripts.Keys.Should().StartWith(1);
        scripts.Keys.Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        scripts[1].Should().Contain("CREATE TABLE games");
    }
}
