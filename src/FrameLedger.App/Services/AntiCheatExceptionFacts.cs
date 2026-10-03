// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>
/// D33: what the exception's disclosure names — the game, the anti-cheat family and its signal. (D38 took the session count
/// out: a grant needs no session, and its first two are a trial the disclosure describes.)
/// </summary>
public sealed record AntiCheatExceptionFacts(string GameName, string Family, string Signal);
