// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Infrastructure.Startup;

/// <summary>What Task Scheduler holds for the Agent's logon task.</summary>
public enum LogonTaskState
{
    /// <summary>No task of that name.</summary>
    NotInstalled = 0,

    /// <summary>A task of that name whose action is this install's Agent.</summary>
    Installed,

    /// <summary>A task of that name whose action points somewhere else (a moved install) — Repair rewrites it.</summary>
    Stale,

    /// <summary><c>schtasks</c> could not be asked.</summary>
    Unknown,
}
