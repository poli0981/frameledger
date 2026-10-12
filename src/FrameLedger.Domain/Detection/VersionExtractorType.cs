// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Detection;

/// <summary>How an engine's version is recovered once its rule matched.</summary>
public enum VersionExtractorType
{
    /// <summary>PE <c>FileVersion</c> of a named sibling file.</summary>
    PeFileVersion,

    /// <summary>First capture group of a regex over the executable's PE <c>ProductVersion</c>.</summary>
    PeProductVersionRegex,

    /// <summary>First capture group of a regex over the bounded strings scan.</summary>
    StringsRegex,

    /// <summary>A field in a store manifest. Not collected this phase.</summary>
    ManifestField,

    /// <summary>
    /// Unreal Engine's own build facts (beta.10): the shipping executable's numeric file version and the engine's branch
    /// name (<c>++UE5+Release-5.4</c>) inside it, weighed by <see cref="UnrealVersion.Decide"/>. Not the evaluator's to
    /// answer — it needs a second read of the executable, which <c>StaticGameDetector</c> makes only for the engine that
    /// matched — so <see cref="RuleEvaluator"/> returns null for it.
    /// </summary>
    UnrealBuild,

    /// <summary>
    /// Unity's (beta.19): the player library's version resource and the data file's header, weighed by
    /// <see cref="UnityVersion.Decide"/>. A second read, like <see cref="UnrealBuild"/>.
    /// </summary>
    UnityBuild,

    /// <summary>Godot's (beta.19): a data pack's header and the executable's version resource (<see cref="GodotVersion.Decide"/>).</summary>
    GodotBuild,

    /// <summary>
    /// RPG Maker's (beta.19): MV/MZ's core-script header, or the RGSS library <c>Game.ini</c> names
    /// (<see cref="RpgMakerVersion.Decide"/>).
    /// </summary>
    RpgMakerBuild,

    /// <summary>Ren'Py's (beta.20): the <c>version</c> line of the engine's <c>renpy/vc_version.py</c> (<see cref="RenPyVersion.Decide"/>).</summary>
    RenPyBuild,
}
