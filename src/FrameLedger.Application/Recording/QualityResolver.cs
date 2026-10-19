// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FrameLedger.Application.Capture;
using FrameLedger.Domain.Metrics;
using S = FrameLedger.Application.Capture.NvidiaDriverSettings;

namespace FrameLedger.Application.Recording;

/// <summary>
/// Which quality mode a session ran DLSS at, and on whose authority (beta.24, owner decision D63; <c>03_METRICS</c>
/// §Upscaling). Read at display time from what every session already stores, so a session recorded before reads alike.
/// </summary>
/// <remarks>
/// <para>
/// <b>The NVIDIA App's override is the standard — when the driver confirms it applied.</b> The override is configuration
/// until the driver's per-process report (<c>ngx_driver_words</c>) says it took effect in the game's process: the
/// override ENABLED and its mode, DLAA or ratio bit set, on a session where DLSS super resolution itself ran (a hook named
/// it, or the driver reports the feature created and evaluated). The mode is then the profile's (<c>NGX_DLSS_SR_MODE</c>,
/// documented), checked against the driver's own report where both say something. When they disagree, or the report
/// alone carries a value its numbering does not settle, the session reads as overridden with no mode — never the game's
/// own value, which the override replaced.
/// </para>
/// <para>
/// <b>Otherwise the mode the game passed to a hooked call</b> (the Streamline byte, <see cref="UpscalerNames.Mode"/>; a
/// value no table names is kept as stored), and otherwise none. Nothing here infers a mode from the render scale
/// (<c>HANDOFF</c> §7a).
/// </para>
/// </remarks>
public static class QualityResolver
{
    /// <summary>The live card's token for an override whose mode the driver's report alone does not settle.</summary>
    public const string OverrideToken = "override";

    /// <summary>A stored session's mode, or null when none is known.</summary>
    /// <param name="upscaler">The row's upscaler token (what a hook named).</param>
    /// <param name="quality">The row's hooked quality byte.</param>
    /// <param name="driverReported">The row's driver-reported upscaler (<c>dlss</c> when the driver saw the feature run).</param>
    /// <param name="ngxDriverWordsJson">The row's <c>ngx_driver_words</c>.</param>
    /// <param name="driverProfileJson">The row's <c>driver_profile</c>.</param>
    public static ResolvedQuality? Of(string? upscaler, string? quality, string? driverReported, string? ngxDriverWordsJson, string? driverProfileJson)
    {
        bool hookNamed = upscaler is { Length: > 0 } and not "unknown";
        NgxDriverWords? words = NgxDriverWords.Parse(ngxDriverWordsJson);
        if (DlssRan(upscaler, driverReported) && words is { Outcome: nameof(NgxProbeOutcome.Answered) } && Applied(Mask(words.Sr)))
        {
            return Override(Mask(words.Sr), words.Mode, DriverProfileRecord.Parse(driverProfileJson));
        }

        if (!hookNamed || string.IsNullOrWhiteSpace(quality))
        {
            return null;
        }

        return UpscalerNames.Mode(upscaler, quality) is { } mode
            ? new ResolvedQuality(mode, QualitySource.Game)
            : new ResolvedQuality(null, QualitySource.Game, Stored: quality);
    }

    /// <summary>
    /// The live card's override from the driver's report alone — the profile is read once per session by the recorder and
    /// does not travel with a progress event — so it names what the report settles by itself and marks the rest as an
    /// override with no mode until the stored session resolves it. Null unless DLSS ran and the override applied.
    /// </summary>
    public static ResolvedQuality? Live(bool dlssRan, ulong srMask, uint performanceMode) =>
        dlssRan && Applied(srMask) ? Override(srMask, performanceMode, profile: null) : null;

    /// <summary>
    /// The wire token for an override (<c>SessionProgressEvent.UpscalerQualityOverride</c>): the mode's, or
    /// <see cref="OverrideToken"/> when its mode is not settled; null for no override.
    /// </summary>
    public static string? Token(ResolvedQuality? resolved) =>
        resolved is { Source: QualitySource.NvidiaApp } ? ModeToken(resolved.Mode) ?? OverrideToken : null;

    /// <summary>A mode's invariant token — the wire's and the export's —, or null for none.</summary>
    public static string? ModeToken(UpscalerQualityMode? mode) => mode switch
    {
        UpscalerQualityMode.Performance => "performance",
        UpscalerQualityMode.Balanced => "balanced",
        UpscalerQualityMode.Quality => "quality",
        UpscalerQualityMode.UltraPerformance => "ultra_performance",
        UpscalerQualityMode.UltraQuality => "ultra_quality",
        UpscalerQualityMode.Dlaa => "dlaa",
        UpscalerQualityMode.Custom => "custom",
        _ => null,
    };

    /// <summary>A wire token back as the override it names; null for none and for a token this build does not know.</summary>
    public static ResolvedQuality? FromToken(string? token)
    {
        UpscalerQualityMode? mode = token switch
        {
            "performance" => UpscalerQualityMode.Performance,
            "balanced" => UpscalerQualityMode.Balanced,
            "quality" => UpscalerQualityMode.Quality,
            "ultra_performance" => UpscalerQualityMode.UltraPerformance,
            "ultra_quality" => UpscalerQualityMode.UltraQuality,
            "dlaa" => UpscalerQualityMode.Dlaa,
            "custom" => UpscalerQualityMode.Custom,
            _ => null,
        };
        return mode is not null || string.Equals(token, OverrideToken, StringComparison.Ordinal) ? new ResolvedQuality(mode, QualitySource.NvidiaApp) : null;
    }

    /// <summary>DLSS super resolution ran in the session: a hook named it, or — where no hook named anything — the driver saw it.</summary>
    private static bool DlssRan(string? upscaler, string? driverReported) => upscaler is { Length: > 0 } and not "unknown"
        ? string.Equals(upscaler, "dlss", StringComparison.Ordinal)
        : string.Equals(driverReported, "dlss", StringComparison.Ordinal);

    /// <summary>The driver says the override is on in this process and set the mode (or DLAA, or a ratio) there.</summary>
    private static bool Applied(ulong sr) =>
        (sr & NgxOverrideFlags.Enabled) != 0 && (sr & (NgxOverrideFlags.PerfMode | NgxOverrideFlags.SrDlaaMode | NgxOverrideFlags.ScalingRatio)) != 0;

    private static ResolvedQuality Override(ulong sr, uint? reported, DriverProfileRecord? profile)
    {
        if ((sr & NgxOverrideFlags.SrDlaaMode) != 0)
        {
            return new ResolvedQuality(UpscalerQualityMode.Dlaa, QualitySource.NvidiaApp);
        }

        UpscalerQualityMode? fromReport = (sr & NgxOverrideFlags.PerfMode) != 0 ? UpscalerNames.FromDriverReport(reported) : null;
        UpscalerQualityMode? fromProfile = profile is not null && IsRead(profile) ? UpscalerNames.FromProfile(profile.ValueOf(S.DlssSrMode)) : null;
        if (fromProfile == UpscalerQualityMode.Custom)
        {
            uint? ratio = profile!.ValueOf(S.DlssSrScalingRatio);
            return new ResolvedQuality(UpscalerQualityMode.Custom, QualitySource.NvidiaApp, ratio is >= 33 and <= 100 ? ratio : null);
        }

        // Two NVIDIA sources that disagree about one setting: the override stands, its mode is not shown.
        UpscalerQualityMode? mode = fromProfile is not null && fromReport is not null && fromProfile != fromReport ? null : fromProfile ?? fromReport;
        return new ResolvedQuality(mode, QualitySource.NvidiaApp);
    }

    private static bool IsRead(DriverProfileRecord profile) =>
        profile.Outcome is nameof(DriverProfileOutcome.Application) or nameof(DriverProfileOutcome.Global);

    private static ulong Mask(string? hex) =>
        hex is { Length: > 2 } && ulong.TryParse(hex.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong value) ? value : 0;
}
