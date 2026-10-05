// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>
/// A long-lived piece of the UI that read its text when it was made and must read it again after a language change
/// (beta.15). A language change rebuilds the window and its pages (<see cref="ShellHost.Rebuild"/>), so everything transient
/// follows on its own; a singleton — the tray's menu, the title bar's Agent pill — kept the language it started in until
/// its state next changed. <see cref="ShellHost.Rebuild"/> calls every registered follower once the new window is up.
/// </summary>
public interface ILanguageFollower
{
    /// <summary>Reads every text again in the current UI culture. Called on the UI thread.</summary>
    void FollowLanguage();
}
