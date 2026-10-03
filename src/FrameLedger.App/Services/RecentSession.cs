// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Services;

/// <summary>A session with its game's name, for the Dashboard's recent list (rows carry only a game id).</summary>
public sealed record RecentSession(SessionRow Row, string GameName);
