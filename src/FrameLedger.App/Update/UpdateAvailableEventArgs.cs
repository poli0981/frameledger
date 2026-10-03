// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Update;

/// <summary>A newer release was found (<c>08_UI</c> §Notifications policy: "update available" is a tray toast when the window is off screen).</summary>
public sealed class UpdateAvailableEventArgs(string version) : EventArgs
{
    public string Version { get; } = version ?? throw new ArgumentNullException(nameof(version));
}
