// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Watch;

/// <summary>What the orchestrator needs to know that no port tells it.</summary>
public sealed record OrchestratorOptions
{
    /// <summary>The Overlay beside the guard (§S22); the Agent's composition root supplies it.</summary>
    public required string PayloadPath { get; init; }

    /// <summary>The watcher's cadence; <c>01_ARCHITECTURE</c> §Lifecycle says 1 Hz and ~0 % CPU.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// True for an executable whose install root is a GDK package root (beta.33, D80): <c>LaunchGame</c> answers
    /// <see cref="LaunchOutcome.PackagedGame"/> for it rather than start it by its path — started that way it is never the game
    /// in its package (<c>spike-notes</c> §25). Null: nothing is treated as packaged (tests, the console's election).
    /// </summary>
    public Func<string, bool>? IsPackaged { get; init; }
}
