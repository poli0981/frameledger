// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.ViewModels;

/// <summary>The library's state filter (beta.34, D85): the card's one state, as its pill says it.</summary>
public enum GamesStateFilter
{
    All = 0,
    HookingOn,
    HookingOff,
    AntiCheat,
    Exception,
    NotRecorded,
}
