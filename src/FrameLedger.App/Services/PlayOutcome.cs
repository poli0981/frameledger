// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>What one press of Play did.</summary>
public enum PlayOutcome
{
    /// <summary>Nobody decided. The zero value, never a result.</summary>
    NotEvaluated = 0,

    /// <summary>The store's app has the game (<see cref="PlayResult.Store"/> names the store).</summary>
    HandedToStore,

    /// <summary>The capture agent accepted the launch (<c>LaunchGame</c>).</summary>
    StartedByAgent,

    /// <summary>Direct, and no capture agent is connected.</summary>
    AgentUnavailable,

    /// <summary>Direct, and the entry's recording is off: FrameLedger starts only what it records (schema 0009).</summary>
    RecordingOff,

    /// <summary>A session of the game is already running.</summary>
    AlreadyRunning,

    /// <summary>Windows would not open the store's app, or the agent refused for another reason (<see cref="PlayResult.Detail"/>).</summary>
    Failed,
}
