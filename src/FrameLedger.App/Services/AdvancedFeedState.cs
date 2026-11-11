// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>What <see cref="AdvancedMachineFeed"/> last learnt from the Agent about "this PC, now" (owner decision D101).</summary>
public enum AdvancedFeedState
{
    /// <summary>Nobody watches: nothing is asked, and the Agent's reader is not held for the page.</summary>
    Stopped,

    /// <summary>Asked, and the Agent's reader has not finished its first read.</summary>
    Waiting,

    /// <summary>The last request was answered with a snapshot.</summary>
    Live,

    /// <summary>The Agent answered that Advanced metrics is off: it reads nothing.</summary>
    Off,

    /// <summary>No Agent answered — not connected, a viewer, a request that timed out or failed. What was shown stays, as old as it is.</summary>
    NoAgent,

    /// <summary>The Agent is an older one that does not know the request.</summary>
    OlderAgent,
}
