// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Detection;

/// <summary>
/// What a Godot title's own files say about the engine it was exported with (beta.19), read from the files on disk.
/// </summary>
/// <remarks>
/// A Godot export's data pack starts with a header — the magic <c>GDPC</c>, the pack format, then the engine's MAJOR, MINOR
/// and PATCH — whether it sits beside the executable (<c>Game.pck</c>) or is appended to it. Measured 2026-10-12 on the
/// owner's two Godot installs: Plethora of Bullets 2 (format 2, 4.2.2) and 衔蝶 (format 2, 4.4.1). The executable's
/// own <c>Godot Engine v4.4.1.stable.official</c> string sits 57 to 78 MB into it — past the 8 MiB the rules' strings scan
/// reads, which is why the old <c>strings_regex</c> extractor never found it.
/// </remarks>
public sealed record GodotBuildFacts
{
    /// <summary>The engine's MAJOR, MINOR and PATCH from a data pack's header, or null when no header was read.</summary>
    public IReadOnlyList<int>? PackVersion { get; init; }

    /// <summary>True when that header was read from a pack appended to the executable, not from a <c>.pck</c> beside it.</summary>
    public bool PackEmbedded { get; init; }

    /// <summary>The executable's <c>ProductName</c>, or null.</summary>
    public string? ExeProductName { get; init; }

    /// <summary>The executable's <c>ProductVersion</c>, or null.</summary>
    public string? ExeProductVersion { get; init; }
}
