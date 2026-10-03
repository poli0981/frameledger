// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Watch;

/// <summary>What one poll of <see cref="ProcessWatcher"/> noticed about a tracked game's process.</summary>
/// <param name="Pid">The process the event is about.</param>
public abstract record WatchEvent(int Pid);
