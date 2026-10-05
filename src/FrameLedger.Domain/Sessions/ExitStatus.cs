// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Sessions;

/// <summary><c>04_CAPTURE</c> §Crash &amp; exit classification, and <c>sessions.exit_status</c>'s CHECK list.</summary>
public enum ExitStatus
{
    /// <summary>The presenting process exited 0 and no Application Error / WER record names it.</summary>
    Normal = 0,

    /// <summary>
    /// An exception's exit code (<c>ExitStatusMapper.IsExceptionCode</c> — "non-zero" until beta.8, corrected 2026-10-05), an
    /// Application-log crash report of this game (<c>CrashEventMatcher</c>, beta.14), or an engine's crash reporter the game
    /// started before it left (<c>CrashReporterRule</c>, beta.14).
    /// </summary>
    Crashed,

    /// <summary>The guard fired mid-session.</summary>
    UnhookedSafety,

    /// <summary>The Overlay self-disabled after faults, stopped itself, or the supervision was lost.</summary>
    Degraded,

    /// <summary>The Agent died mid-session; recovered from the <c>.partial</c> on the next start.</summary>
    Interrupted,
}
