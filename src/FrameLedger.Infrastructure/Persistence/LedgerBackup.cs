// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Infrastructure.Startup;
using Microsoft.Data.Sqlite;

namespace FrameLedger.Infrastructure.Persistence;

/// <summary>
/// The alpha line's copy of the ledger it shares with the beta (owner decision D73, 2026-10-31; <c>06_DATA_MODEL</c>
/// §Migrations): before an alpha build first opens a ledger, the ledger is copied whole into
/// <c>backups\ledger-before-&lt;version&gt;.db</c> with SQLite's online backup, once per alpha version, and the newest three
/// copies are kept. A copy that cannot be made refuses the open (<see cref="LedgerBackupException"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own lock, not the migration's.</b> The App and the Agent can open the ledger at the same moment, so the copy is
/// taken under <see cref="LockNameFor"/>, waited on for up to five minutes: the migration lock gives a waiter 30 s and
/// then throws (<see cref="MigrationRunner"/>), and a large ledger on a slow disk can take longer to copy than that. The
/// second opener finds the copy made and goes on.
/// </para>
/// <para>
/// <b>Synchronous throughout.</b> <see cref="SharedMutex"/> is held by the thread that took it; the backup is SQLite's
/// own, a read-only connection to the ledger copied into a new file that becomes the copy only when complete.
/// </para>
/// </remarks>
public static class LedgerBackup
{
    private const string _mutexPrefix = @"Local\FrameLedger.Ledger.Backup.";
    private const string _prefix = "ledger-before-";
    private const string _extension = ".db";

    /// <summary>How long a second opener waits for the first one's copy.</summary>
    public static TimeSpan LockWait { get; } = TimeSpan.FromMinutes(5);

    /// <summary>The copy's file name for <paramref name="version"/>.</summary>
    public static string FileNameFor(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        char[] invalid = Path.GetInvalidFileNameChars();
        return _prefix + string.Concat(version.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c)) + _extension;
    }

    /// <summary>One lock per ledger file, keyed as the migration's is (<see cref="MigrationRunner.LockNameFor"/>).</summary>
    public static string LockNameFor(string databasePath)
    {
        string migration = MigrationRunner.LockNameFor(databasePath);
        return _mutexPrefix + migration[(migration.AsSpan().LastIndexOf('.') + 1)..];
    }

    /// <summary>
    /// Copies <paramref name="databasePath"/> as <paramref name="request"/> asks, unless this version's copy exists or there
    /// is no ledger yet (a first start has nothing to lose). Returns the copy's path when this call made it.
    /// </summary>
    /// <exception cref="LedgerBackupException">The copy could not be made; the ledger must not be opened.</exception>
    public static string? EnsureCopied(string databasePath, LedgerBackupRequest request, Action<string>? diagnostics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentNullException.ThrowIfNull(request);
        string full = Path.GetFullPath(databasePath);
        var ledger = new FileInfo(full);
        if (!ledger.Exists || ledger.Length == 0)
        {
            return null;
        }

        string target = Path.Combine(request.Directory, FileNameFor(request.Version));
        using SharedMutex mutex = SharedMutex.CreateOrOpen(LockNameFor(full));
        bool held = mutex.Wait(LockWait, out _);
        try
        {
            if (!held)
            {
                throw new LedgerBackupException($"another process has been copying {full} for over {LockWait.TotalMinutes:0} minutes; this alpha build does not open the ledger without its copy");
            }

            if (File.Exists(target))
            {
                return null;
            }

            return Copy(full, target, request, diagnostics);
        }
        finally
        {
            if (held)
            {
                mutex.Release();
            }
        }
    }

    private static string Copy(string full, string target, LedgerBackupRequest request, Action<string>? diagnostics)
    {
        string partial = target + ".partial";
        try
        {
            System.IO.Directory.CreateDirectory(request.Directory);
            long needed = SizeWithWal(full);
            long free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(target))!).AvailableFreeSpace;
            if (free < needed + (needed / 4))
            {
                throw new LedgerBackupException($"the copy of {full} needs about {needed / (1024 * 1024)} MB and {Path.GetPathRoot(target)} has {free / (1024 * 1024)} MB free; this alpha build does not open the ledger without its copy");
            }

            File.Delete(partial);
            using (var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = full, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
            using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = partial, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
            }

            File.Move(partial, target);
            diagnostics?.Invoke($"ledger: copied to {target} before this alpha build first opened it ({needed / 1024} KB)");
            Prune(request, target);
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            TryDelete(partial);
            throw new LedgerBackupException($"{full} could not be copied to {target} before this alpha build opened it ({ex.Message}); it does not open the ledger without its copy", ex);
        }
    }

    /// <summary>
    /// The copy just made and the newest others, <see cref="LedgerBackupRequest.Keep"/> in all, stay; only files this
    /// mechanism names are touched. The one just made is never a candidate, whatever a skewed clock wrote on the others.
    /// </summary>
    private static void Prune(LedgerBackupRequest request, string made)
    {
        FileInfo[] others = [.. new DirectoryInfo(request.Directory).GetFiles(_prefix + "*" + _extension)
            .Where(f => !string.Equals(f.FullName, Path.GetFullPath(made), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(static f => f.LastWriteTimeUtc)];
        foreach (FileInfo old in others.Skip(Math.Max(0, request.Keep - 1)))
        {
            TryDelete(old.FullName);
        }
    }

    private static long SizeWithWal(string full)
    {
        var wal = new FileInfo(full + "-wal");
        return new FileInfo(full).Length + (wal.Exists ? wal.Length : 0);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A copy that cannot be deleted now is deleted by the next alpha's prune; a partial one is overwritten.
        }
    }
}
