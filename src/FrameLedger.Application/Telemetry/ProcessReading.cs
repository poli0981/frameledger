// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Application.Telemetry;

/// <summary>
/// The memory the game's own process uses on one tick (beta.12, owner decision D43; <c>03_METRICS</c> §Game process
/// memory) — read from outside the game, never from its memory: the video half from the <c>GPU Process Memory</c>
/// performance counters, the system half from <c>GetProcessMemoryInfo</c>. Every field nullable; null is <c>N/A</c>,
/// never zero. MiB throughout (1 GB = 1024 MiB, Task Manager's counting).
/// </summary>
/// <param name="VramDedicatedMb">Dedicated GPU memory — Task Manager's Details column of that name. Not the DXGI budget-model "local usage": it reads a few MiB more per device (spike-notes §16, M3), and it is never compared with a budget.</param>
/// <param name="VramSharedMb">Shared GPU memory — system memory the GPU uses on the process's behalf.</param>
/// <param name="RamPrivateMb">The private working set — Task Manager's "Memory" column. Null where Windows predates <c>PROCESS_MEMORY_COUNTERS_EX2</c>.</param>
/// <param name="RamWorkingSetMb">The whole working set, shared pages included.</param>
/// <param name="CommitMb">The commit charge (<c>PrivateUsage</c>): private memory committed, resident or not.</param>
/// <param name="Processes">How many processes the figures are summed over: 1 for a pinned game; every process running the game's executable for an unpinned Tier-2 hold.</param>
/// <param name="Sources">Which reads answered, for <c>sessions.game_memory_source</c>.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ProcessReading(
    double? VramDedicatedMb,
    double? VramSharedMb,
    double? RamPrivateMb,
    double? RamWorkingSetMb,
    double? CommitMb,
    int Processes,
    ProcessReadingSources Sources)
{
    /// <summary>No field carries a value: the tick has nothing to say about the game's memory.</summary>
    public bool IsEmpty => VramDedicatedMb is null && VramSharedMb is null && RamPrivateMb is null && RamWorkingSetMb is null && CommitMb is null;
}
