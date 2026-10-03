// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Application.Recording;

/// <summary>
/// What finalizing did. <see cref="GameId"/> is the row the session was stored under when that is known — the one it
/// started under, or the row that holds its executable's path when that one was removed while it ran (2026-09-23).
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct FinalizeOutcome(FinalizeStatus Status, long? SessionId, int RetentionSwept, long? GameId = null);
