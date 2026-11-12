// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// What became, in one session, of the entries read from ONE call that FrameLedger hooks only for Advanced metrics
/// (0.1.0-beta.43: <c>slSetConstants</c>, <c>ffxConfigure</c>). Beside <see cref="AdvancedInGameStatus"/>, which
/// speaks for the session as a whole, this tells "the hook was in and the game never made the call" from "the hook
/// was not in" - two different reasons a value is absent, and only the first says something about the game.
/// </summary>
public enum AdvancedInGameCall
{
    /// <summary>Not stated: a row stored before 0.1.0-beta.43.</summary>
    Unknown = 0,

    /// <summary>The game made the call and what it passed was read.</summary>
    Read = 1,

    /// <summary>The hook was in for the session and the game did not make the call where it could be seen.</summary>
    NeverCalled = 2,

    /// <summary>
    /// The hook was not in: the game did not load the library that exports the call (it does not use Streamline, or
    /// FSR through AMD's libraries), or the library is of a generation FrameLedger does not read.
    /// </summary>
    NotHooked = 3,
}
