// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Recording;

/// <summary>Where a session's quality mode came from (beta.24, D63).</summary>
public enum QualitySource
{
    /// <summary>Nobody named a source. The zero value, so a default is not a claim.</summary>
    None = 0,

    /// <summary>The mode the game passed to a hooked call.</summary>
    Game = 1,

    /// <summary>The NVIDIA App's override, which the driver reported applied in the game's process.</summary>
    NvidiaApp = 2,
}
