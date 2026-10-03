// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Capture;

namespace FrameLedger.Application.Telemetry;

/// <summary>
/// Which process the telemetry thread reads the game's memory of (beta.12, D43). The capture loop owns the process's
/// identity and tells the poller (<see cref="ICaptureObserver.Measuring"/> → <see cref="ITelemetryPoller.Follow"/>); the
/// poller only reads what it is told, so the rules about what may be opened stay in one place — <c>CaptureSession</c>.
/// </summary>
public sealed record GameProcess
{
    private GameProcess(int? pid, ITargetLiveness? pinned, IReadOnlyList<GameProcessId> processes)
    {
        Pid = pid;
        PinnedBy = pinned;
        Processes = processes;
    }

    /// <summary>Nothing to read: before a session has a process, and after the one it had was let go.</summary>
    public static GameProcess None { get; } = new(null, null, []);

    /// <summary>The pinned process. Its handle is the session's own — held for liveness — so the read opens nothing.</summary>
    public int? Pid { get; }

    /// <summary>The session's pin on <see cref="Pid"/>, whose handle the reader may use for the system half.</summary>
    public ITargetLiveness? PinnedBy { get; }

    /// <summary>An unpinned Tier-2 hold's processes — every one running the game's executable in the watcher's snapshot.</summary>
    public IReadOnlyList<GameProcessId> Processes { get; }

    public bool IsNone => Pid is null && Processes.Count == 0;

    /// <summary>A process the session holds a handle to: the attach and launch paths, and a refusal after the pin.</summary>
    public static GameProcess Pinned(int pid, ITargetLiveness pinnedBy)
    {
        ArgumentNullException.ThrowIfNull(pinnedBy);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pid);
        return new GameProcess(pid, pinnedBy, []);
    }

    /// <summary>
    /// The processes of a hold that never opened the game — hooking off, blocked, unresolved, cannot pin. The reader opens
    /// each transiently with the least right that answers and refuses one whose creation time is not the snapshot's.
    /// </summary>
    public static GameProcess Unpinned(IReadOnlyList<GameProcessId> processes)
    {
        ArgumentNullException.ThrowIfNull(processes);
        return processes.Count == 0 ? None : new GameProcess(null, null, [.. processes]);
    }

    /// <summary>Whether two unpinned targets name the same processes (a hold re-announces only a changed set).</summary>
    public bool SameProcessesAs(IReadOnlyList<GameProcessId> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Pid is null && Processes.Count == other.Count && Processes.All(other.Contains);
    }
}
