// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>
/// <c>07_IPC</c> §Client behavior: "on failure start the Agent, retry". The Agent is the one beside this
/// executable — <c>12_BUILD</c> publishes both roots into one directory — and nothing else; the App never
/// searches for one.
/// </summary>
public interface IAgentLauncher
{
    /// <summary>Whether an Agent executable is beside this one at all.</summary>
    bool CanLaunch { get; }

    /// <summary>
    /// Why starting an Agent now would start a SECOND one, or null when none is up: an Agent already holds this user's data
    /// folder (the logon task's, an earlier App's, a console's), or the one this App started last has not exited and not yet
    /// claimed it. 2026-09-17: an App that could not connect started one per round, and four recorded every session.
    /// </summary>
    string? RunningAgent { get; }

    /// <summary>
    /// Whether <see cref="TryStart"/> would show Windows' administrator prompt (beta.10, the admin mode on and this App a
    /// standard user's) — the pill says so while the round waits for the answer. A launcher that never asks answers false.
    /// </summary>
    bool WillAskForElevation => false;

    /// <summary>Start <c>FrameLedger.Agent.exe --serve</c>, detached; false when it could not be started.</summary>
    bool TryStart();
}
