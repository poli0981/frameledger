// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Application.Telemetry;

/// <summary>One process of an unpinned hold: its pid and the creation time the watcher's snapshot read.</summary>
/// <param name="Pid">The process id.</param>
/// <param name="StartedAt">The creation time from the snapshot; a reused pid's differs, and the read is refused.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct GameProcessId(int Pid, DateTimeOffset StartedAt);
