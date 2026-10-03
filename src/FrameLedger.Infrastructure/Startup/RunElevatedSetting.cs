// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using Microsoft.Data.Sqlite;

namespace FrameLedger.Infrastructure.Startup;

/// <summary>
/// Whether the user turned on the Agent's admin mode (<c>capture.run_elevated</c>, beta.10, owner decision D34), read BEFORE
/// the Agent starts or claims anything: by the App deciding how to start it, and by an Agent the logon task started deciding
/// whether to ask Windows for elevation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not through <see cref="Persistence.LedgerDatabase"/>.</b> Its open migrates, or — read-only — refuses a ledger at
/// another schema; the first start after an update meets exactly that ledger, and the answer here must not depend on who
/// migrates first. One query on one row, <c>PRAGMA query_only</c>, nothing written.
/// </para>
/// <para>
/// <b>Everything that is not an explicit "1" is off</b> — no ledger yet, no settings table, a locked or unreadable file: the
/// Agent then starts as it always did, with a standard user's rights. Off is the default the owner chose.
/// </para>
/// </remarks>
public static class RunElevatedSetting
{
    /// <summary>The registry key (<c>Application.Settings.SettingsRegistry.CaptureRunElevated</c>).</summary>
    public const string Key = "capture.run_elevated";

    /// <summary>True only when the ledger at <paramref name="databasePath"/> says exactly "1".</summary>
    public static bool Read(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        if (!File.Exists(databasePath))
        {
            return false;
        }

        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWrite,    // never Create: a missing ledger is "off", not a new file
                Pooling = false,
                DefaultTimeout = 2,
            }.ToString());
            connection.Open();
            using (SqliteCommand readOnly = connection.CreateCommand())
            {
                readOnly.CommandText = "PRAGMA query_only = 1";
                readOnly.ExecuteNonQuery();
            }

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM settings WHERE key = $key";
            command.Parameters.AddWithValue("$key", Key);
            return command.ExecuteScalar() is string value && string.Equals(value, "1", StringComparison.Ordinal);
        }
        catch (SqliteException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
