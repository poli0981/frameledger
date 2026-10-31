// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Infrastructure.Persistence;

/// <summary>What an alpha build asks of <see cref="LedgerDatabase.OpenAsync"/> before it opens the ledger (D73).</summary>
/// <param name="Directory">Where the copies go — <see cref="LedgerPaths.BackupsDirectoryOf"/>.</param>
/// <param name="Version">The build's version, which names the copy: one copy per alpha version.</param>
/// <param name="Keep">How many copies are kept, newest first; older ones this mechanism wrote are deleted.</param>
public sealed record LedgerBackupRequest(string Directory, string Version, int Keep = 3);
