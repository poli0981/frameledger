// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Detection;

/// <summary>
/// Where an engine version came from (beta.10, <c>games.engine_version_source</c>): the ids are stored, so they never change
/// meaning. The App says which witness a version rests on, because two of them can be wrong in different ways.
/// </summary>
public static class EngineVersionSource
{
    /// <summary>Unreal: the branch name and the numeric file version agree on MAJOR.MINOR — the patch is the file's.</summary>
    public const string UnrealBranchAndFile = "unreal_branch_file";

    /// <summary>Unreal: the branch name alone (the file version is the studio's own) — MAJOR.MINOR, no patch.</summary>
    public const string UnrealBranch = "unreal_branch";

    /// <summary>Unreal: the numeric file version alone, where nothing suggests the studio replaced it.</summary>
    public const string UnrealFileVersion = "unreal_file_version";

    /// <summary>Unity (beta.19): the player library's <c>ProductVersion</c>, an editor version with its release letter.</summary>
    public const string UnityPlayer = "unity_player";

    /// <summary>Unity: the editor version the data file's header names.</summary>
    public const string UnityData = "unity_data";

    /// <summary>Unity: the player library's numeric file version — MAJOR.MINOR.PATCH, no release letter.</summary>
    public const string UnityPlayerFile = "unity_player_file";

    /// <summary>Godot (beta.19): the header of the data pack beside the executable.</summary>
    public const string GodotPack = "godot_pck";

    /// <summary>Godot: the header of the data pack appended to the executable.</summary>
    public const string GodotPackEmbedded = "godot_pck_embedded";

    /// <summary>Godot: the executable's version resource, still the engine's (<c>ProductName</c> is "Godot Engine").</summary>
    public const string GodotExe = "godot_exe";

    /// <summary>RPG Maker MZ (beta.19): the header of <c>js/rmmz_core.js</c>.</summary>
    public const string RpgMakerMzCore = "rpgmaker_mz_core";

    /// <summary>RPG Maker MV: the header of <c>rpg_core.js</c>.</summary>
    public const string RpgMakerMvCore = "rpgmaker_mv_core";

    /// <summary>RPG Maker XP: the RGSS1 library <c>Game.ini</c> names.</summary>
    public const string Rgss1Library = "rgss1_library";

    /// <summary>RPG Maker VX: the RGSS2 library.</summary>
    public const string Rgss2Library = "rgss2_library";

    /// <summary>RPG Maker VX Ace: the RGSS3 library.</summary>
    public const string Rgss3Library = "rgss3_library";

    /// <summary>The rule's own extractor, named as the rules file names it (<c>pe_file_version</c>, <c>strings_regex</c>, …).</summary>
    public static string Of(VersionExtractorType type) => type switch
    {
        VersionExtractorType.PeFileVersion => "pe_file_version",
        VersionExtractorType.PeProductVersionRegex => "pe_product_version_regex",
        VersionExtractorType.StringsRegex => "strings_regex",
        VersionExtractorType.ManifestField => "manifest_field",
        VersionExtractorType.UnrealBuild => "unreal_build",
        VersionExtractorType.UnityBuild => "unity_build",
        VersionExtractorType.GodotBuild => "godot_build",
        VersionExtractorType.RpgMakerBuild => "rpgmaker_build",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "not a version extractor"),
    };
}
