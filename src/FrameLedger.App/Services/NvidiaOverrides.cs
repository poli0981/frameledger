using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Recording;
using S = FrameLedger.Application.Capture.NvidiaDriverSettings;

namespace FrameLedger.App.Services;

/// <summary>
/// What the NVIDIA driver was told to do with a game, in words (beta.8, owner request 2026-09-25): the NVIDIA App's DLSS and
/// frame-generation overrides as the driver profile stores them (<c>sessions.driver_profile</c>), and what the driver itself
/// reported applying in the game's process (<c>sessions.ngx_driver_words</c>).
/// </summary>
/// <remarks>
/// A configuration and a driver's report — never a measurement. What a session MEASURED (the upscaler a hook saw, the frame
/// generation it counted) stays the hooks' and is shown as such; these lines say what the driver was set to do and what it
/// says it did (<c>03_METRICS</c> §Upscaling). The meanings of the values are NVIDIA's <c>NvApiDriverSettings.h</c>.
/// </remarks>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public static class NvidiaOverrides
{
    /// <summary>
    /// The session summary's line for the profile: which profile the driver applied and what it overrides. Null when there
    /// is nothing to say — no profile read (no NVIDIA driver, or a row from before beta.8).
    /// </summary>
    public static string? ProfileLine(string? driverProfileJson)
    {
        DriverProfileRecord? profile = DriverProfileRecord.Parse(driverProfileJson);
        if (profile is null || !IsRead(profile))
        {
            return null;
        }

        string overrides = OverridesText(profile);
        return string.Equals(profile.Outcome, nameof(DriverProfileOutcome.Application), StringComparison.Ordinal)
            ? string.Format(CultureInfo.CurrentCulture, Strings.Nv_Profile_Format, profile.Profile ?? Strings.Common_NotAvailable, overrides)
            : string.Format(CultureInfo.CurrentCulture, Strings.Nv_Profile_Global_Format, overrides);
    }

    /// <summary>What the profile overrides, as one phrase list — or "no DLSS or frame generation override".</summary>
    public static string OverridesText(DriverProfileRecord profile)
    {
        IReadOnlyList<string> parts = Of(profile);
        return parts.Count == 0 ? Strings.Nv_Override_None : string.Join(" · ", parts);
    }

    /// <summary>Whether the record holds a profile that was read (an application's or the global one).</summary>
    public static bool IsRead(DriverProfileRecord profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return profile.Outcome is nameof(DriverProfileOutcome.Application) or nameof(DriverProfileOutcome.Global);
    }

    /// <summary>The overrides a profile sets, one phrase each; empty when it sets none.</summary>
    public static IReadOnlyList<string> Of(DriverProfileRecord profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        List<string> parts = [];
        if (profile.ValueOf(S.DlssSrOverride) == 1)
        {
            parts.Add(Detailed(Strings.Nv_Override_Sr, Preset(profile.ValueOf(S.DlssSrPreset)),
                Mode(profile.ValueOf(S.DlssSrMode), profile.ValueOf(S.DlssSrScalingRatio)),
                profile.ValueOf(S.DlaaOverride) == 1 ? Strings.Nv_Mode_Dlaa : null));
        }

        if (profile.ValueOf(S.DlssRrOverride) == 1)
        {
            parts.Add(Detailed(Strings.Nv_Override_Rr, Preset(profile.ValueOf(S.DlssRrPreset)),
                Mode(profile.ValueOf(S.DlssRrMode), profile.ValueOf(S.DlssRrScalingRatio))));
        }

        if (profile.ValueOf(S.DlssFgOverride) == 1)
        {
            parts.Add(Detailed(Strings.Nv_Override_Fg, Multiplier(profile.ValueOf(S.DlssgMultiFrameCount)), Preset(profile.ValueOf(S.DlssFgPreset)),
                FrameGenerationMode(profile.ValueOf(S.DlssgMode)), DynamicTarget(profile.ValueOf(S.DlssgDynamicTargetFrameRate))));
        }
        else if (FrameGenerationMode(profile.ValueOf(S.DlssgMode)) is { } forced)
        {
            parts.Add(forced);
        }

        if (profile.ValueOf(S.StreamlineDlssOverride) == 1)
        {
            parts.Add(Strings.Nv_Override_Sl);
        }

        if (profile.ValueOf(S.SmoothMotion) == 1)
        {
            parts.Add(Strings.Nv_SmoothMotion);
        }

        return parts;
    }

    /// <summary>
    /// The session summary's line for the driver's own report on the game's process (<c>NvAPI_NGX_GetNGXOverrideState</c>):
    /// what its override bits say it applied. Null when it answered nothing, or reported no override.
    /// </summary>
    public static string? DriverLine(string? ngxDriverWordsJson)
    {
        if (string.IsNullOrWhiteSpace(ngxDriverWordsJson))
        {
            return null;
        }

        NgxDriverWords? words;
        try
        {
            words = JsonSerializer.Deserialize(ngxDriverWordsJson, RecordingJsonContext.Default.NgxDriverWords);
        }
        catch (JsonException)
        {
            return null;
        }

        if (words is null || !string.Equals(words.Outcome, nameof(NgxProbeOutcome.Answered), StringComparison.Ordinal))
        {
            return null;
        }

        ulong sr = Mask(words.Sr);
        ulong fg = Mask(words.Fg);
        List<string> parts = [];
        if ((sr & NgxOverrideFlags.DllSelected) != 0)
        {
            parts.Add(Strings.Nv_Driver_DllSelected);
        }

        if ((sr & NgxOverrideFlags.Preset) != 0 && Preset(words.Preset) is { } preset)
        {
            parts.Add(preset);
        }

        if ((sr & NgxOverrideFlags.PerfMode) != 0 && Mode(words.Mode, ratio: null) is { } mode)
        {
            parts.Add(mode);
        }

        if ((sr & NgxOverrideFlags.ScalingRatio) != 0 && words.Ratio is float ratio && ratio > 0)
        {
            parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.Nv_Driver_Ratio_Format, ratio.ToString("0.###", CultureInfo.CurrentCulture)));
        }

        if ((sr & NgxOverrideFlags.SrDlaaMode) != 0)
        {
            parts.Add(Strings.Nv_Mode_Dlaa);
        }

        if ((fg & NgxOverrideFlags.FgMultiFrame) != 0 && Multiplier(words.FgCount) is { } multiplier)
        {
            parts.Add(Detailed(Strings.Nv_Override_Fg, multiplier));
        }

        return parts.Count == 0 ? null : string.Format(CultureInfo.CurrentCulture, Strings.Nv_Driver_Format, string.Join(" · ", parts));
    }

    private static ulong Mask(string? hex) =>
        hex is { Length: > 2 } && ulong.TryParse(hex.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong value) ? value : 0;

    private static string Detailed(string head, params string?[] details)
    {
        string[] present = [.. details.Where(static d => !string.IsNullOrEmpty(d)).Select(static d => d!)];
        return present.Length == 0 ? head : string.Format(CultureInfo.CurrentCulture, Strings.Nv_Details_Format, head, string.Join(", ", present));
    }

    /// <summary>A render preset: 1 = A … 26 = Z, "latest", "default"; 0 (off) and anything else say nothing.</summary>
    public static string? Preset(uint? value) => value switch
    {
        null or 0 => null,
        S.PresetLatest => Strings.Nv_Preset_Latest,
        S.PresetDefault => Strings.Nv_Preset_Default,
        >= 1 and <= 26 => string.Format(CultureInfo.CurrentCulture, Strings.Nv_Preset_Format, (char)('A' + (int)value.Value - 1)),
        _ => null,
    };

    /// <summary><c>NGX_DLSS_SR_MODE</c> / <c>_RR_MODE</c>; 3 is "the game's own" — no override — and says nothing.</summary>
    public static string? Mode(uint? value, uint? ratio) => value switch
    {
        0 => Strings.Nv_Mode_Performance,
        1 => Strings.Nv_Mode_Balanced,
        2 => Strings.Nv_Mode_Quality,
        4 => Strings.Nv_Mode_Dlaa,
        5 => Strings.Nv_Mode_UltraPerformance,
        6 => ratio is >= 33 and <= 100
            ? string.Format(CultureInfo.CurrentCulture, Strings.Nv_Mode_Custom_Format, ratio.Value)
            : Strings.Nv_Mode_Custom,
        _ => null,
    };

    /// <summary><c>NGX_DLSSG_MULTI_FRAME_COUNT</c>: frames generated per rendered frame, said as the multiplier (1 = ×2 … 3 = ×4).</summary>
    public static string? Multiplier(uint? generated) =>
        generated is >= 1 and <= 15 ? string.Format(CultureInfo.CurrentCulture, Strings.Nv_Mfg_Format, generated.Value + 1) : null;

    private static string? FrameGenerationMode(uint? value) => value switch
    {
        1 => Strings.Nv_FgMode_Off,
        2 => Strings.Nv_FgMode_On,
        3 => Strings.Nv_FgMode_Auto,
        4 => Strings.Nv_FgMode_Dynamic,
        _ => null,
    };

    private static string? DynamicTarget(uint? value) => value switch
    {
        null or 0 => null,
        S.DynamicTargetAuto => Strings.Nv_DynamicTarget_Auto,
        < S.DynamicTargetAuto => string.Format(CultureInfo.CurrentCulture, Strings.Nv_DynamicTarget_Format, value.Value),
        _ => null,
    };
}
