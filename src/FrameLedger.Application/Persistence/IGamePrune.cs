// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Persistence;

/// <summary>
/// Removes a library entry whose game is no longer installed (beta.19, owner decision D55) — the second Agent write that
/// deletes a <c>games</c> row, after the twin merge (<see cref="IGameMerge"/>; <c>06_DATA_MODEL</c> §Writer ownership).
/// </summary>
public interface IGamePrune
{
    /// <summary>
    /// Deletes the entry when, in the same statement, it is still at <paramref name="exePath"/>, still in the library, has
    /// no session stored, and carries no anti-cheat block, exception or failed-trial mark (CLAUDE.md rule 2: nothing clears
    /// a block). True when it was deleted; false when any condition no longer held — nothing is written then.
    /// </summary>
    ValueTask<bool> RemoveUninstalledAsync(long gameId, string exePath, CancellationToken ct = default);
}
