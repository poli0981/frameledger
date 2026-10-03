// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>The Dashboard's totals strip (<c>08_UI</c> §Dashboard): games tracked, total playtime, sessions in the last seven days.</summary>
public sealed record LibraryTotals(int GamesTracked, double TotalSeconds, long SessionsThisWeek);
