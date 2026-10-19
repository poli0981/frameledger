// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>
/// An upscaler's quality mode by the name its menu gives it (beta.24, owner decision D63): the mode a hooked call carried
/// and the mode an NVIDIA App override set are the same names, so every surface says a mode alike whoever chose it. Each
/// source numbers them its own way, and <see cref="UpscalerNames"/> decodes each where it is read.
/// </summary>
/// <remarks>A mode nobody decoded is a null; the zero member exists so a default is not a claim, never Performance.</remarks>
public enum UpscalerQualityMode
{
    /// <summary>Nobody named a mode. The zero value, so a default is not a claim; never stored or shown.</summary>
    None = 0,

    Performance = 1,
    Balanced = 2,
    Quality = 3,
    UltraPerformance = 4,
    UltraQuality = 5,
    Dlaa = 6,

    /// <summary>An override's custom render-scale ratio; its percentage travels beside it.</summary>
    Custom = 7,
}
