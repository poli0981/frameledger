// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>
/// Mirror of <c>FlHookFamily</c>: which hook families a writer installed. Monotonic — bits are only ever
/// set. Ray tracing's definite "No" requires <see cref="RtAsBuild"/> to have been <i>installed</i>, not
/// merely for RT to have been "measured".
/// </summary>
[Flags]
public enum HookFamilies
{
    None = 0,
    Present = 1 << 0,
    UpscalerIdentity = 1 << 1,
    UpscalerParams = 1 << 2,
    FgEvaluations = 1 << 3,
    RtDispatch = 1 << 4,
    RtAsBuild = 1 << 5,
    RtPso = 1 << 6,
    Pso = 1 << 7,
    ColorSpace = 1 << 8,
    Reflex = 1 << 9,
    Vram = 1 << 10,

    /// <summary>The FidelityFX quality query (beta.25, D65).</summary>
    UpscalerQuality = 1 << 11,

    /// <summary>The Streamline detour decodes DLSS 5's neural rendering (beta.34, D84).</summary>
    SlNeuralRendering = 1 << 12,

    /// <summary><c>slSetConstants</c> is hooked (0.1.0-beta.43) - only where Advanced metrics was ever asked for.</summary>
    SlConstants = 1 << 13,
}
