// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Play;

/// <summary>Where GOG GALAXY's program is (beta.21, Play): the GOG game shortcuts' own command needs it.</summary>
public interface IGogGalaxyLocator
{
    /// <summary>The full path of <c>GalaxyClient.exe</c> as GOG's own registry entries name it, or null when it is not installed.</summary>
    string? ClientPath();
}
