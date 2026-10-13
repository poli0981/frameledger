// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>
/// How Play hands a game to its store's app (beta.21, D59): a link the shell opens with the app registered for it, or the
/// store's own program. Behind a port so a test starts nothing and a viewer (D52) cannot (<see cref="ViewerShellStarter"/>).
/// </summary>
public interface IShellStarter
{
    /// <summary>Opens <paramref name="link"/> with the app Windows has for its scheme; whether Windows took it.</summary>
    bool OpenLink(string link);

    /// <summary>Starts <paramref name="path"/> with <paramref name="arguments"/> in <paramref name="workingDirectory"/>; whether it started.</summary>
    bool StartProgram(string path, string arguments, string? workingDirectory);
}
