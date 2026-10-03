// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Telemetry;

namespace FrameLedger.Application.Capture;

/// <summary>How the host finds the process to capture. There is no pid argument anywhere.</summary>
public interface ITargetResolver
{
    /// <summary>
    /// The single running process whose image is <paramref name="normalisedExePath"/>.
    /// </summary>
    /// <returns>The pid, or null with <paramref name="reason"/> set.</returns>
    int? Resolve(string normalisedExePath, out SessionEndReason reason);

    /// <summary>
    /// Whether the executable is running (2026-09-22). The Tier-2 hold's clock for a target the loop never opened: a game
    /// whose hooking is off is never opened at all, and a process the resolver could not read cannot be pinned. Where a
    /// watcher runs it answers from the watcher's snapshot by the IMAGE PATH (2026-09-23 — by file name, another game's
    /// <c>Game.exe</c> kept an RPG Maker game's hold open), a process whose path could not be read still counting by its
    /// name; the console verbs, where nothing polls, keep the name. Nothing is ever injected on the strength of this answer;
    /// it only decides how long duration and telemetry accrue.
    /// </summary>
    bool IsRunning(string normalisedExePath);

    /// <summary>
    /// The processes running <paramref name="normalisedExePath"/> by their IMAGE PATH in the watcher's snapshot (beta.10) —
    /// never by file name, the mistake 2026-09-23 fixed for the hold's clock — so a Tier-2 hold can find the game's window
    /// without opening a process. Empty where no watcher runs, none matched, or every match's path was unreadable: the
    /// display sample then says "no window", never another program's.
    /// </summary>
    IReadOnlyList<int> PidsOf(string normalisedExePath);

    /// <summary>
    /// <see cref="PidsOf"/> with each process's creation time from the same snapshot (beta.12, D43): what an unpinned hold
    /// reads the game's memory of, so a pid reused between the snapshot and the read is refused rather than read off a
    /// stranger. A process whose creation time could not be read is left out. Empty where no watcher runs.
    /// </summary>
    IReadOnlyList<GameProcessId> ProcessesOf(string normalisedExePath) => [];
}
