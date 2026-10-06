// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>What the status pill and the banner show (08_UI §Shell: "Agent state shown as a pill").</summary>
public enum AgentConnectionState
{
    /// <summary>A connect round is in progress.</summary>
    Connecting = 0,

    /// <summary>No Agent answered; it was started from beside this executable and a new round runs.</summary>
    Starting,

    /// <summary><c>Hello</c> answered.</summary>
    Connected,

    /// <summary>An Agent exists beside this executable and is not answering; rounds continue.</summary>
    Offline,

    /// <summary>No Agent executable is beside this one; nothing to start, rounds continue in case one appears.</summary>
    Missing,

    /// <summary>
    /// The Agent is being started as administrator (beta.10, the admin mode): Windows' prompt is up, owned by this window or
    /// flashing on the taskbar, and the round waits for its answer.
    /// </summary>
    Elevating,

    /// <summary>
    /// A viewer over a copy of a data folder (beta.15, D52: <c>--data-dir</c>): no Agent is started or contacted, ever —
    /// the state never changes.
    /// </summary>
    Viewer,
}
