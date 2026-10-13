// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>Why a press in the store mode was started directly instead.</summary>
public enum PlayFallback
{
    None = 0,

    /// <summary>No store's records name the folder the executable is in.</summary>
    NoStore,

    /// <summary>GOG's records name it, and GOG GALAXY is not installed.</summary>
    NoGalaxy,

    /// <summary>The App runs as administrator, and a store's app it started would too — and so would the game.</summary>
    Elevated,
}
