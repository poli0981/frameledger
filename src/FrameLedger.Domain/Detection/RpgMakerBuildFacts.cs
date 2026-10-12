// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Detection;

/// <summary>
/// What an RPG Maker title's own files say about the maker that built it (beta.19), read from the files on disk.
/// </summary>
/// <remarks>
/// <para>
/// <b>MV and MZ</b> ship their engine as JavaScript, and its core script opens with its own name and version — line 2 of
/// <c>js/rmmz_core.js</c> reads <c>// rmmz_core.js v1.10.0</c> (A Doll's Locket - Remake), of <c>www/js/rpg_core.js</c>
/// <c>// rpg_core.js v1.6.2</c> (Flower in Us, 996IR) or <c>v1.6.1</c> (HELLO, HELLO WORLD!) — measured 2026-10-12.
/// </para>
/// <para>
/// <b>XP, VX and VX Ace</b> run on the Ruby Game Scripting System, whose library <c>Game.ini</c> names
/// (<c>Library=System\RGSS301.dll</c>): RGSS1 is XP, RGSS2 VX, RGSS3 VX Ace, and the name carries the library's version
/// (<c>RGSS301</c> is 3.0.1; its version resource reads <c>3, 0, 1, 1</c> on City of Bones and Träumerei Chapter 2).
/// </para>
/// </remarks>
public sealed record RpgMakerBuildFacts
{
    /// <summary>The core script whose header was read — <c>rmmz_core.js</c> or <c>rpg_core.js</c> — or null.</summary>
    public string? CoreScript { get; init; }

    /// <summary>The version that script's header names (<c>1.10.0</c>), or null.</summary>
    public string? CoreScriptVersion { get; init; }

    /// <summary>The RGSS library's file name (<c>RGSS301.dll</c>), as <c>Game.ini</c> names it or as found, or null.</summary>
    public string? RgssLibrary { get; init; }

    /// <summary>That library's numeric file version (four parts), or null when it carries none or was not read.</summary>
    public IReadOnlyList<int>? RgssFileVersion { get; init; }
}
