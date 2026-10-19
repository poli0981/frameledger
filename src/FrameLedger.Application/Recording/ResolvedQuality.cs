// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Domain.Metrics;

namespace FrameLedger.Application.Recording;

/// <summary>A session's quality mode as <see cref="QualityResolver"/> settled it (beta.24, D63).</summary>
/// <param name="Mode">The mode, by its menu name; null only for a hooked value no table names (<paramref name="Stored"/>).</param>
/// <param name="Source">Whose it is: the game's own call, or the NVIDIA App's override the driver confirmed.</param>
/// <param name="CustomPercent">An override's custom render-scale percentage (33–100), when it set one.</param>
/// <param name="Stored">A hooked value no table names, as stored: still what was measured, so it is shown as it is.</param>
public sealed record ResolvedQuality(UpscalerQualityMode? Mode, QualitySource Source, uint? CustomPercent = null, string? Stored = null);
