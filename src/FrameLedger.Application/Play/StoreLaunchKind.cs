// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Play;

/// <summary>How Play hands a game to its store's app (beta.21, owner decision D59).</summary>
public enum StoreLaunchKind
{
    /// <summary>Nobody decided. The zero value, never a launch.</summary>
    None = 0,

    /// <summary>A link the shell opens with the store's registered app (<c>steam://</c>, <c>com.epicgames.launcher://</c>, <c>itch://</c>).</summary>
    OpenLink,

    /// <summary>The store's own program with arguments — GOG GALAXY's <c>/command=runGame</c>, as its shortcuts start a game.</summary>
    RunProgram,
}
