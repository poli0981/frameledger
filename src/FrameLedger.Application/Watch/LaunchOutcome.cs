// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Watch;

/// <summary>What <see cref="CaptureOrchestrator.LaunchAsync"/> decided.</summary>
public enum LaunchOutcome
{
    /// <summary>Nobody decided. The zero value, never a result.</summary>
    NotEvaluated = 0,

    /// <summary>The session task is running; <see cref="LaunchResult.SessionGuid"/> names it.</summary>
    Accepted,

    /// <summary>No <c>games</c> row has that id.</summary>
    UnknownGame,

    /// <summary>A session for that game is already running (one at a time).</summary>
    SessionRunning,

    /// <summary>This orchestrator was composed without launch mode (the console's election-only instance, or a test).</summary>
    Unavailable,

    /// <summary>The entry's recording is switched off (schema 0009, 2026-09-23): FrameLedger does not start what it will not record.</summary>
    RecordingOff,

    /// <summary>
    /// The entry's executable is in a game the Xbox app or the Microsoft Store installed (beta.33, D80): started by its path it
    /// is either handed back to its package by Windows or runs outside it (<c>spike-notes</c> §25), so FrameLedger never starts
    /// it that way; Windows starts it, from Play or its Start-menu tile, and the watcher records it.
    /// </summary>
    PackagedGame,
}
