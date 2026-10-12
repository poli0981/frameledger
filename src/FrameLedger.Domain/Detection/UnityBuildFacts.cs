// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Detection;

/// <summary>
/// What a Unity title's own files say about the engine it was built with (beta.19): read from the files on disk, never from
/// the game's process (CLAUDE.md rule 4 is about game memory).
/// </summary>
/// <remarks>
/// Measured read-only on the owner's fifteen Unity installs, 2026-10-12 (<c>05_DETECTION</c> §Engine version — Unity, Godot,
/// RPG Maker): the player library's <c>ProductVersion</c> names the editor version in thirteen
/// (<c>2022.3.39f1 (4e1b0f82c39a)</c>); a 2019.4 player carries only numbers there, and its data file's header names it
/// (<c>2019.4.1f1</c>); GIRLS' FRONTLINE 2's data file is encrypted, and its player's numeric file version
/// (<c>2019.4.40.16762411</c>) is all there is.
/// </remarks>
public sealed record UnityBuildFacts
{
    /// <summary><c>UnityPlayer.dll</c>'s <c>ProductVersion</c> string, or null.</summary>
    public string? PlayerProductVersion { get; init; }

    /// <summary><c>UnityPlayer.dll</c>'s numeric file version (four parts), or null when it carries none or was not read.</summary>
    public IReadOnlyList<int>? PlayerFileVersion { get; init; }

    /// <summary>The editor version the header of <c>&lt;Game&gt;_Data\globalgamemanagers</c> names, as read, or null.</summary>
    public string? DataHeaderVersion { get; init; }
}
