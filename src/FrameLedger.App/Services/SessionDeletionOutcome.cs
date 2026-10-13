// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>How a <c>DeleteSessions</c> ask ended (beta.11, D40).</summary>
public enum SessionDeletionOutcome
{
    /// <summary>The Agent deleted them; the counts say how many.</summary>
    Deleted,

    /// <summary>A session it would delete under is being recorded: nothing was deleted.</summary>
    SessionRunning,

    /// <summary>The capture agent is not connected: nothing was asked.</summary>
    AgentUnavailable,

    /// <summary>The Agent does not know the request (an older build): nothing was deleted.</summary>
    AgentTooOld,

    /// <summary>Anything else; the detail says what.</summary>
    Failed,

    /// <summary>
    /// beta.22: the capture agent did not answer (<see cref="AgentAnswers.IsUnanswered"/>), or answered without saying what it
    /// did: whether anything was deleted is not known, and the page reads what is stored.
    /// </summary>
    Unanswered,
}
