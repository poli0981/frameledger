// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>
/// The session summary windows that are open (beta.11, owner decision D40). After Delete all sessions a window left open
/// would show a session that is gone, and a note written from it would go to whichever session reuses its id (the table has
/// no AUTOINCREMENT).
/// </summary>
public interface ISessionWindows
{
    /// <summary>Closes every open summary window whose session is no longer in the ledger.</summary>
    Task CloseDeletedAsync(CancellationToken ct = default);
}
