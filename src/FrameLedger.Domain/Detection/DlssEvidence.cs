// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Detection;

/// <summary>What a session observed about DLSS, for <see cref="DlssGeneration.Of"/> (beta.24, D64; beta.34, D83).</summary>
/// <param name="SuperResolution">The loaded <c>nvngx_dlss.dll</c>'s file version.</param>
/// <param name="FrameGeneration">The loaded <c>nvngx_dlssg.dll</c>'s file version.</param>
/// <param name="Preset">The super-resolution render preset the NVIDIA driver reports applied in a session where it ran (1 = A …).</param>
/// <param name="LargestFrameGenerationFactor">The largest DLSS Frame Generation state the session measured.</param>
/// <param name="DynamicFrameGeneration">
/// The NVIDIA App's <i>Dynamic</i> frame generation ran in the session (beta.34, D83): the game's driver profile sets the
/// Dynamic mode, the driver reports its frame-generation override applied in the game's process, and DLSS Frame Generation
/// was counted. Dynamic exists only in DLSS 4.5, whatever multiplier it settled on.
/// </param>
public sealed record DlssEvidence(Version? SuperResolution, Version? FrameGeneration, uint? Preset, double? LargestFrameGenerationFactor,
    bool DynamicFrameGeneration = false);
