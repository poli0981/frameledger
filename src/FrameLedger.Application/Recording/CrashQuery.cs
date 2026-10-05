// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Recording;

/// <summary>
/// What a crash witness is asked about one session (beta.14): the executable, the pid the session held (0 when it held
/// none), the window <c>[start, end + grace]</c>, and whether the session ended because the game itself left — a crash
/// reporter only counts for a game that exited.
/// </summary>
public sealed record CrashQuery(string ExePath, int TargetPid, DateTimeOffset WindowStart, DateTimeOffset WindowEnd, bool TargetLeft)
{
    /// <summary>The executable's file name, the form the event log names it by.</summary>
    public string ExeFileName => Path.GetFileName(ExePath);
}
