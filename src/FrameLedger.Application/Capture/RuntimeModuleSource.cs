// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Capture;

/// <summary>
/// Where a loaded runtime module's file lives, by its folder (beta.24, owner decision D64) — the class is stored, the
/// path never is (<c>sessions.runtime_module_sources</c>).
/// </summary>
public enum RuntimeModuleSource
{
    /// <summary>Not classified: a set read before beta.24, or a module whose path could not be read.</summary>
    Unknown = 0,

    /// <summary>Anywhere outside Windows' folder and the graphics driver's: the game's own copy, or one beside it.</summary>
    Game = 1,

    /// <summary>
    /// The graphics driver's own copy: Windows' driver store, or <c>%ProgramData%\NVIDIA</c> — where the NVIDIA App's
    /// DLSS override keeps the libraries it substitutes.
    /// </summary>
    GraphicsDriver = 2,

    /// <summary>Windows' folder outside the driver store.</summary>
    Windows = 3,
}
