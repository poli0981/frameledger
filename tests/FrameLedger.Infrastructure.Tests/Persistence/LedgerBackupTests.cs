// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using Dapper;
using FluentAssertions;
using FrameLedger.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace FrameLedger.Infrastructure.Tests.Persistence;

/// <summary>
/// D73 (2026-10-31): an alpha build copies the ledger it shares with the beta before it first opens it, once per alpha
/// version, keeps the newest three copies, and does not open the ledger when the copy cannot be made. Every case runs in a
/// scratch folder: no test reaches the profile's ledger (HANDOFF traps).
/// </summary>
public sealed class LedgerBackupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-ledger-backup-" + Guid.NewGuid().ToString("N"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Ledger => Path.Combine(_dir, LedgerPaths.DatabaseFileName);

    private string Backups => LedgerPaths.BackupsDirectoryOf(Ledger);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task TheFirstOpenOfAnAlphaVersionCopiesTheLedgerWithItsRowsAndTheSecondDoesNot()
    {
        await CreateLedgerWithARowAsync().ConfigureAwait(true);
        var request = new LedgerBackupRequest(Backups, "0.1.0-alpha.1");
        List<string> lines = [];

        LedgerDatabase db = await LedgerDatabase.OpenAsync(Ledger, diagnostics: lines.Add, backupFirst: request, ct: Ct).ConfigureAwait(true);
        await db.DisposeAsync().ConfigureAwait(true);

        string copy = Path.Combine(Backups, "ledger-before-0.1.0-alpha.1.db");
        File.Exists(copy).Should().BeTrue();
        (await ReadValueAsync(copy).ConfigureAwait(true)).Should().Be("before the alpha", "the copy is the whole ledger, WAL included");
        lines.Should().Contain(static l => l.StartsWith("ledger: copied to ", StringComparison.Ordinal));
        DateTime written = File.GetLastWriteTimeUtc(copy);

        LedgerBackup.EnsureCopied(Ledger, request).Should().BeNull("one copy per alpha version: the second opener finds it made");
        File.GetLastWriteTimeUtc(copy).Should().Be(written);
    }

    [Fact]
    public void AFirstStartWithNoLedgerHasNothingToCopy()
    {
        Directory.CreateDirectory(_dir);

        LedgerBackup.EnsureCopied(Ledger, new LedgerBackupRequest(Backups, "0.1.0-alpha.1")).Should().BeNull();

        Directory.Exists(Backups).Should().BeFalse("nothing is written for a ledger that does not exist yet");
    }

    [Fact]
    public async Task OnlyTheNewestThreeCopiesAreKeptAndNothingElseInTheFolderIsTouched()
    {
        await CreateLedgerWithARowAsync().ConfigureAwait(true);
        Directory.CreateDirectory(Backups);
        string mine = Path.Combine(Backups, "notes.txt");
        await File.WriteAllTextAsync(mine, "the user's", Ct).ConfigureAwait(true);

        int age = 10;
        foreach (string version in new[] { "0.1.0-alpha.1", "0.1.0-alpha.2", "0.1.0-alpha.3", "0.1.0-alpha.4" })
        {
            LedgerBackup.EnsureCopied(Ledger, new LedgerBackupRequest(Backups, version)).Should().NotBeNull();
            // Each copy older than the next, as alphas installed one after another leave them.
            File.SetLastWriteTimeUtc(Path.Combine(Backups, LedgerBackup.FileNameFor(version)), DateTime.UtcNow.AddMinutes(-age--));
        }

        Directory.GetFiles(Backups, "ledger-before-*.db").Select(Path.GetFileName).Should().BeEquivalentTo(
            ["ledger-before-0.1.0-alpha.2.db", "ledger-before-0.1.0-alpha.3.db", "ledger-before-0.1.0-alpha.4.db"]);
        File.Exists(mine).Should().BeTrue("the prune deletes only the copies it names");
    }

    [Fact]
    public async Task TheCopyJustMadeIsKeptEvenWhenOlderCopiesCarryLaterTimes()
    {
        await CreateLedgerWithARowAsync().ConfigureAwait(true);
        Directory.CreateDirectory(Backups);
        foreach (string version in new[] { "0.1.0-alpha.1", "0.1.0-alpha.2", "0.1.0-alpha.3" })
        {
            string path = Path.Combine(Backups, LedgerBackup.FileNameFor(version));
            await File.WriteAllTextAsync(path, "an older copy", Ct).ConfigureAwait(true);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(1));
        }

        string? made = LedgerBackup.EnsureCopied(Ledger, new LedgerBackupRequest(Backups, "0.1.0-alpha.4"));

        made.Should().NotBeNull();
        File.Exists(made).Should().BeTrue("a clock that ran ahead must not cost the copy just made");
        Directory.GetFiles(Backups, "ledger-before-*.db").Should().HaveCount(3);
    }

    [Fact]
    public async Task ACopyThatCannotBeMadeRefusesTheOpen()
    {
        await CreateLedgerWithARowAsync().ConfigureAwait(true);
        // A file where the backups folder must be: the copy cannot be written.
        await File.WriteAllTextAsync(Backups, "in the way", Ct).ConfigureAwait(true);

        Func<Task> open = async () => await LedgerDatabase.OpenAsync(Ledger, backupFirst: new LedgerBackupRequest(Backups, "0.1.0-alpha.1"), ct: Ct).ConfigureAwait(false);

        (await open.Should().ThrowAsync<LedgerBackupException>().ConfigureAwait(true))
            .Which.Message.Should().Contain("does not open the ledger without its copy");
    }

    [Fact]
    public async Task WithoutARequestNothingIsCopied()
    {
        await CreateLedgerWithARowAsync().ConfigureAwait(true);

        LedgerDatabase db = await LedgerDatabase.OpenAsync(Ledger, ct: Ct).ConfigureAwait(true);
        await db.DisposeAsync().ConfigureAwait(true);

        Directory.Exists(Backups).Should().BeFalse("the main line opens as it always has");
    }

    [Theory]
    [InlineData("0.1.0-alpha.1", "ledger-before-0.1.0-alpha.1.db")]
    [InlineData("1.2.3", "ledger-before-1.2.3.db")]
    [InlineData("a/b:c", "ledger-before-a_b_c.db")]
    public void TheCopyIsNamedForTheVersion(string version, string name) => LedgerBackup.FileNameFor(version).Should().Be(name);

    [Fact]
    public void TheLockIsTheLedgersOwnAndNotTheMigrations()
    {
        string name = LedgerBackup.LockNameFor(Ledger);

        name.Should().StartWith(@"Local\FrameLedger.Ledger.Backup.");
        name.Should().NotBe(MigrationRunner.LockNameFor(Ledger));
        string migration = MigrationRunner.LockNameFor(Ledger);
        name[(name.AsSpan().LastIndexOf('.') + 1)..].Should().Be(migration[(migration.AsSpan().LastIndexOf('.') + 1)..], "keyed by the same file");
    }

    private async Task CreateLedgerWithARowAsync()
    {
        LedgerDatabase db = await LedgerDatabase.OpenAsync(Ledger, ct: Ct).ConfigureAwait(true);
        await using (db.ConfigureAwait(true))
        {
            await db.WriteAsync((c, tx, ct) => c.ExecuteAsync(new CommandDefinition("INSERT INTO settings (key, value) VALUES ('t.backup', 'before the alpha')", transaction: tx, cancellationToken: ct)), Ct).ConfigureAwait(true);
        }
    }

    private static async Task<string?> ReadValueAsync(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await using (connection.ConfigureAwait(true))
        {
            await connection.OpenAsync().ConfigureAwait(true);
            return await connection.ExecuteScalarAsync<string?>("SELECT value FROM settings WHERE key = 't.backup'").ConfigureAwait(true);
        }
    }
}
