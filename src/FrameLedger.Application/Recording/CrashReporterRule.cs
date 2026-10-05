// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Recording;

/// <summary>
/// When a crash reporter the game started counts as its crash (beta.14). It must have started at least
/// <see cref="StartedWithTheGame"/> after the game — a reporter that starts with its game is a monitor, not a crash, and a
/// crash in the game's first seconds is under-reported rather than a monitor fabricating one — and the game must have left
/// within <see cref="ExitWithin"/> of it: a game that kept running reported an error it survived, which is a note and not
/// a crash.
/// </summary>
public static class CrashReporterRule
{
    public static readonly TimeSpan StartedWithTheGame = TimeSpan.FromSeconds(10);

    public static readonly TimeSpan ExitWithin = TimeSpan.FromSeconds(60);

    public static bool Counts(CrashReporterSighting sighting, CrashQuery query, DateTimeOffset endedAt)
    {
        ArgumentNullException.ThrowIfNull(sighting);
        ArgumentNullException.ThrowIfNull(query);
        bool afterStart = sighting.ParentStartedAt is not { } gameStarted || sighting.StartedAt - gameStarted >= StartedWithTheGame;
        return query.TargetLeft && afterStart && endedAt - sighting.StartedAt <= ExitWithin;
    }
}
