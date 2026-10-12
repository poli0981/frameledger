// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>FR-1.1's file pick, behind an interface so a test can answer it without a dialog.</summary>
public interface IGamePicker
{
    /// <summary>The chosen executable's full path, or null when the user cancelled.</summary>
    /// <param name="initialDirectory">
    /// The folder the dialog opens in (beta.19: Change executable… opens in the game's own folder, <see cref="GameFolder"/>);
    /// null leaves it to Windows, which opens where the last pick was.
    /// </param>
    string? PickExecutable(string? initialDirectory = null);
}
