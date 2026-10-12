// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Domain.Detection;

namespace FrameLedger.Application.Detection;

/// <summary>Collects everything the evaluator is allowed to know about a game.</summary>
/// <remarks>
/// Takes the rule set because the snapshot is <strong>rules-dependent</strong>:
/// the strings pass has to know which needles and regexes to look for before it
/// walks, so this is not a general-purpose picture of a directory. Anything the
/// probe could not establish must come back in
/// <see cref="GameFileSnapshot.UncollectedFacts"/> rather than as an absence.
/// </remarks>
public interface IGameFileProbe
{
    /// <summary>Walks the game directory once and returns what it found.</summary>
    ValueTask<GameFileSnapshot> SnapshotAsync(string exePath, DetectionRuleSet rules, CancellationToken ct = default);

    /// <summary>
    /// Unreal Engine's build facts from the title's shipping executable (beta.10) — asked only after the rules matched an
    /// engine whose extractor is <see cref="VersionExtractorType.UnrealBuild"/>, because answering can mean reading a large
    /// file. Null when no shipping executable is in the snapshot's listing or it could not be read: no facts, never a guess.
    /// </summary>
    ValueTask<UnrealBuildFacts?> ReadUnrealBuildAsync(GameFileSnapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// Unity's build facts (beta.19) — the player library's version resource and the header of its data file — asked only
    /// after the rules matched an engine whose extractor is <see cref="VersionExtractorType.UnityBuild"/>. Null when neither
    /// file is there or could be read.
    /// </summary>
    ValueTask<UnityBuildFacts?> ReadUnityBuildAsync(GameFileSnapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// Godot's build facts (beta.19) — a data pack's header, beside the executable or appended to it, and the executable's
    /// version resource — for <see cref="VersionExtractorType.GodotBuild"/>. Null when nothing could be read.
    /// </summary>
    ValueTask<GodotBuildFacts?> ReadGodotBuildAsync(GameFileSnapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// RPG Maker's build facts (beta.19) — MV/MZ's core-script header, or the RGSS library <c>Game.ini</c> names — for
    /// <see cref="VersionExtractorType.RpgMakerBuild"/>. Null when neither is found.
    /// </summary>
    ValueTask<RpgMakerBuildFacts?> ReadRpgMakerBuildAsync(GameFileSnapshot snapshot, CancellationToken ct = default);
}
