// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>
/// The state of each source the Agent reads advanced metrics from, at one moment.
/// </summary>
/// <param name="Nvapi">NVAPI: every <c>nvapi.</c> entry whose reach is the Agent.</param>
/// <param name="NvapiCode">The status NVAPI's initialisation answered when <paramref name="Nvapi"/> is not <see cref="AdvancedSourceStatus.Reading"/>; null otherwise.</param>
/// <param name="Sensors">LibreHardwareMonitor's graphics-card group, which needs no rights: the <c>lhm.gpu.</c> entries.</param>
/// <param name="ElevatedSensors">Its processor, memory and motherboard groups, which need the Agent as administrator and PawnIO (D103).</param>
public sealed record AdvancedSources(
    AdvancedSourceStatus Nvapi,
    int? NvapiCode,
    AdvancedSourceStatus Sensors,
    AdvancedSourceStatus ElevatedSensors)
{
    /// <summary>
    /// What became of the entries read inside the game in the session this describes (0.1.0-beta.40); null where
    /// that is not a question — the machine's live state — and in every row stored before it existed.
    /// </summary>
    public AdvancedInGameStatus? InGame { get; init; }

    /// <summary>
    /// What became of what the game tells Streamline about each frame (<c>slSetConstants</c>, the
    /// <c>sl.constants.</c> entries) in the session this describes (0.1.0-beta.43); null where that is not a
    /// question - the machine's live state, a session not measured inside the game - and in every row stored before.
    /// </summary>
    public AdvancedInGameCall? StreamlineConstants { get; init; }

    /// <summary>The same for how the game configures FSR frame generation (<c>ffxConfigure</c>, <c>ffx.framegen.config</c>).</summary>
    public AdvancedInGameCall? FsrFrameGeneration { get; init; }

    /// <summary>
    /// The same for which version each Streamline feature is (<c>slGetFeatureVersion</c>, <c>sl.feature.versions</c>;
    /// 0.1.0-beta.44). A game asks while it starts: <see cref="AdvancedInGameCall.NeverCalled"/> is what a session
    /// attached after that says, and it means "not asked again", not "never asked".
    /// </summary>
    public AdvancedInGameCall? StreamlineFeatureVersion { get; init; }

    /// <summary>The same for what each feature requires (<c>slGetFeatureRequirements</c>, <c>sl.feature.requirements</c>).</summary>
    public AdvancedInGameCall? StreamlineFeatureRequirements { get; init; }

    /// <summary>Nothing asked yet.</summary>
    public static AdvancedSources Unknown { get; } = new(AdvancedSourceStatus.Unknown, NvapiCode: null, AdvancedSourceStatus.Unknown, AdvancedSourceStatus.Unknown);
}
