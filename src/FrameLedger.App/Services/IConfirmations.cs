// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>The confirmations a page asks (<c>08_UI</c> §UX rules: every destructive action confirms), behind an interface for the tests.</summary>
public interface IConfirmations
{
    Task<RemoveGameChoice> RemoveGameAsync(string gameName, CancellationToken ct = default);

    /// <summary>
    /// beta.11 (D40): delete every session of <paramref name="gameName"/>'s game — or of every game when it is null —
    /// <paramref name="sessions"/> of them; true only on the explicit delete button.
    /// </summary>
    Task<bool> DeleteSessionsAsync(string? gameName, long sessions, CancellationToken ct = default);
}
