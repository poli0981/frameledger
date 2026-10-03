// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Persistence;

namespace FrameLedger.Application.Watch;

/// <summary>A process the watcher had matched to a tracked game is no longer in the snapshot.</summary>
/// <param name="Pid">The process that is gone.</param>
/// <param name="Game">The row it had matched.</param>
public sealed record TrackedProcessGone(int Pid, GameRow Game) : WatchEvent(Pid);
