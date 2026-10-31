// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Shared;

namespace FrameLedger.App.Services;

/// <summary>
/// What an alpha build does at the App's start, before the shell (owner decisions D73 and D74, 2026-10-31) — here rather
/// than in <c>App.xaml.cs</c> so that both halves are tested on main, where no alpha is built.
/// </summary>
internal static class AlphaStart
{
    /// <summary>
    /// D73: the copy an alpha makes of the ledger it shares with the beta before it first opens it, once per alpha version
    /// (<see cref="LedgerBackup"/>). None for the main line, and none for a viewer (D52), which reads a copy already.
    /// </summary>
    public static LedgerBackupRequest? BackupFor(BuildLine line, bool viewer, string databasePath, string version)
    {
        ArgumentNullException.ThrowIfNull(line);
        return line.IsAlpha && !viewer ? new LedgerBackupRequest(LedgerPaths.BackupsDirectoryOf(databasePath), version) : null;
    }

    /// <summary>
    /// D74: while an alpha's Legal Gate waits for the Alpha notice, the Agent connection must not start the Agent — it would
    /// refuse to serve and exit, round after round. Called before the host starts, because the connection's first round runs
    /// while the first-run window is up (<see cref="ApplicationHostService"/>); the gate's Accept releases the hold. True when
    /// it held.
    /// </summary>
    public static async Task<bool> HoldTheAgentForTheNoticeAsync(BuildLine line, LegalGate gate, IAgentLink agent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(agent);
        if (!line.IsAlpha)
        {
            return false;
        }

        IReadOnlyList<LegalDocument> outstanding = await gate.OutstandingAsync(ct).ConfigureAwait(true);
        if (!outstanding.Any(static d => string.Equals(d.Key, LegalDocuments.Alpha, StringComparison.Ordinal)))
        {
            return false;
        }

        agent.SetNoticeHold(true);
        return true;
    }
}
