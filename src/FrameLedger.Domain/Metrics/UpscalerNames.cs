// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>
/// The vendors' own quality values as modes (<c>03_METRICS</c> §Upscaling promised this type from the start; until
/// 2026-09-21 the UI printed the raw integer beside the upscaler). Three numberings reach this build, one table each:
/// the hooked byte (Streamline's <c>sl::DLSSMode</c>), the NVIDIA driver's per-process report (NGX's own
/// <c>PerfQuality</c> values) and the NVIDIA App's override as the driver profile stores it (<c>NGX_DLSS_SR_MODE</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The hooked byte.</b> Its only writer is the Streamline route: <c>sl::DLSSMode</c> as the title chained it into
/// <c>slEvaluateFeature</c> (<c>fl_sl_inputs.h</c> <c>QualityFromMode</c>), where <c>eOff</c> and anything at or past
/// <c>eCount</c> are already folded to "not told" and never reach a row. A value this table does not know is not named —
/// the App prints the stored number, because a number nobody can name is still what was measured.
/// </para>
/// <para>
/// <b>The two NVIDIA numberings disagree from 3 up</b>, which is why each has its own table and neither is read through
/// the other (beta.24, D63). Measured on the owner's ledger: Silent Hill 2 under the App's <i>Ultra Performance</i>
/// (profile value 5) was reported by the driver as 3 — NGX's numbering, not the profile's, where 3 is "the game's own".
/// </para>
/// <para>
/// This decodes what was stored. A label derived from the render/output ratio is a different thing and an owner
/// decision not taken (<c>HANDOFF</c> §7a); nothing here infers.
/// </para>
/// </remarks>
public static class UpscalerNames
{
    /// <summary>The hooked byte as a mode, or null when the row stores none or a value this table does not know.</summary>
    /// <param name="upscaler">The row's upscaler token (<c>dlss</c>, <c>fsr3</c>, …).</param>
    /// <param name="quality">The row's quality value: the vendor's integer, invariant.</param>
    public static UpscalerQualityMode? Mode(string? upscaler, string? quality)
    {
        if (string.IsNullOrWhiteSpace(quality) || !string.Equals(upscaler, "dlss", StringComparison.Ordinal))
        {
            return null;
        }

        return quality switch
        {
            "1" => UpscalerQualityMode.Performance,
            "2" => UpscalerQualityMode.Balanced,
            "3" => UpscalerQualityMode.Quality,
            "4" => UpscalerQualityMode.UltraPerformance,
            "5" => UpscalerQualityMode.UltraQuality,
            "6" => UpscalerQualityMode.Dlaa,
            _ => null,
        };
    }

    /// <summary>
    /// The NVIDIA driver's per-process report (<c>NvAPI_NGX_GetNGXOverrideState</c>'s <c>performanceMode</c>) in NGX's own
    /// numbering: 0 Performance, 1 Balanced, 2 Quality, 3 Ultra Performance, 4 Ultra Quality, 5 DLAA. 0–2 agree with the
    /// profile's numbering and 3 is measured (the remarks above); 4 and 5 are NGX's documented values, not yet measured.
    /// </summary>
    public static UpscalerQualityMode? FromDriverReport(uint? performanceMode) => performanceMode switch
    {
        0 => UpscalerQualityMode.Performance,
        1 => UpscalerQualityMode.Balanced,
        2 => UpscalerQualityMode.Quality,
        3 => UpscalerQualityMode.UltraPerformance,
        4 => UpscalerQualityMode.UltraQuality,
        5 => UpscalerQualityMode.Dlaa,
        _ => null,
    };

    /// <summary>
    /// The NVIDIA App's Super Resolution Mode as the driver profile stores it (<c>NGX_DLSS_SR_MODE</c>, NVIDIA's
    /// <c>NvApiDriverSettings.h</c>): 0 Performance, 1 Balanced, 2 Quality, 4 DLAA, 5 Ultra Performance, 6 a custom ratio;
    /// 3 is "the game's own" — no override — and is null, as is anything else.
    /// </summary>
    public static UpscalerQualityMode? FromProfile(uint? srMode) => srMode switch
    {
        0 => UpscalerQualityMode.Performance,
        1 => UpscalerQualityMode.Balanced,
        2 => UpscalerQualityMode.Quality,
        4 => UpscalerQualityMode.Dlaa,
        5 => UpscalerQualityMode.UltraPerformance,
        6 => UpscalerQualityMode.Custom,
        _ => null,
    };
}
