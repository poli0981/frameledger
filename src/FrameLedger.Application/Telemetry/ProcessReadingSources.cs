// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Telemetry;

/// <summary>What answered a <see cref="ProcessReading"/>; OR'd over a session for <c>sessions.game_memory_source</c>.</summary>
[Flags]
public enum ProcessReadingSources
{
    None = 0,

    /// <summary>The <c>GPU Process Memory</c> counters named the process.</summary>
    Counters = 1 << 0,

    /// <summary><c>PROCESS_MEMORY_COUNTERS_EX2</c> answered (the private working set is there).</summary>
    PrivateWorkingSetRead = 1 << 1,

    /// <summary>Only <c>PROCESS_MEMORY_COUNTERS_EX</c> answered: no private working set on this OS.</summary>
    WorkingSetReadOnly = 1 << 2,

    /// <summary>The system half went through the handle the session already held.</summary>
    HeldHandle = 1 << 3,

    /// <summary>The system half went through a handle opened for the read (<c>PROCESS_QUERY_LIMITED_INFORMATION</c>) and closed after it.</summary>
    TransientHandle = 1 << 4,
}
