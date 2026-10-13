// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Play;

/// <summary>
/// One hand-off to a store's app: built by <see cref="StoreLaunches.For"/> from a record the store itself keeps, with every
/// id checked against what that store's ids look like — never from a string a caller or a ledger row hands in, because a
/// games row is a file any program of the user can write and a link is something the shell will open.
/// </summary>
/// <param name="Platform">The store, in <c>games.platform</c>'s vocabulary.</param>
/// <param name="Kind">Whether <paramref name="Target"/> is a link or a program.</param>
/// <param name="Target">The link, or the program's full path.</param>
/// <param name="Arguments">The program's arguments; null for a link.</param>
/// <param name="WorkingDirectory">The program's folder; null for a link.</param>
public sealed record StoreLaunch(string Platform, StoreLaunchKind Kind, string Target, string? Arguments = null, string? WorkingDirectory = null);
