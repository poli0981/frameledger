using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Metrics;
using FrameLedger.Shared;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Services;

/// <summary>
/// CLAUDE.md rule 6 / <c>08_UI</c> §FPS display rule, decided in ONE place: whether a row (or a live progress
/// event) shows <c>62 → 118 FPS (×1.9 FG)</c>, <c>144 FPS</c> alone, or Presented FPS with its census qualifier —
/// and what a table's Native / Displayed / FG× columns say, where <c>—</c> (measured none) and <c>N/A</c> (not
/// measured) are two different negatives that must not collapse.
/// </summary>
/// <remarks>
/// <b>Four shapes since 2026-09-14, not three.</b> A row whose <c>fg_mode</c> names a technology and whose
/// <c>fg_factor</c> is NULL — identity stood, the count refused (<c>fg_refusal</c>) — used to fall into the
/// Generated shape and print <c>N/A → N/A FPS (×0.0 FG)</c>, a factor nobody counted. It is now
/// <see cref="FpsReadoutKind.IdentifiedUncounted"/>: Presented FPS, a warning chip naming the technology, the
/// refusal as the tooltip, and <c>N/A</c> in the Displayed and FG× columns — the number may include generated
/// frames, so "Native" never appears beside it (<c>03_METRICS</c> §Rung 0's qualifier, third row).
/// </remarks>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
public static class FpsPresentation
{
    /// <summary>The row's shape: generated when a factor was counted, none when measured none, identified-uncounted when a technology was named without a factor, presented otherwise.</summary>
    public static FpsReadoutModel FromRow(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Tier != Domain.Sessions.CaptureTier.Hooked || row.FrameCount == 0)
        {
            return FpsReadoutModel.Unavailable;
        }

        return Shape(row.FgMode, row.NativeFps, row.DisplayedFps, row.FgFactor, row.PresentedFps ?? row.NativeFps, ParseQualifier(row.PresentedQualifier), row.FgRefusal, row.FgRuntimeCensus, row.FgRefusalDetail, IsSteady(row) ? row.FgSteadyShare : null);
    }

    /// <summary>The live card's shape from a 1 Hz progress event (<c>07_IPC</c>: the FG fields are set only when measured).</summary>
    public static FpsReadoutModel FromProgress(SessionProgressEvent progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (progress.Presents5s == 0)
        {
            return FpsReadoutModel.Unavailable;
        }

        return Shape(progress.FgMode, progress.NativeFps5s, progress.DisplayedFps5s, progress.FgFactor, progress.PresentedFps5s ?? progress.NativeFps5s, ParseQualifier(progress.PresentedQualifier), progress.FgRefusal, progress.FgRuntimeCensus);
    }

    /// <summary>The table's Native column: the native figure when FG was measured, else the Presented figure (its qualifier is the tooltip).</summary>
    public static string NativeColumn(SessionRow row) => FromRow(row).PrimaryText;

    /// <summary>The table's Displayed column: the figure when FG was counted, <c>—</c> when measured none, <c>N/A</c> when not measured or not counted.</summary>
    public static string DisplayedColumn(SessionRow row)
    {
        FpsReadoutModel m = FromRow(row);
        return m.Kind switch
        {
            FpsReadoutKind.Generated => Formats.Fps(m.Displayed),
            FpsReadoutKind.None => Strings.Common_Dash,
            _ => Strings.Common_NotAvailable,
        };
    }

    /// <summary>The table's FG× column, on the same rule as <see cref="DisplayedColumn"/>: a factor only where one was counted.</summary>
    public static string FactorColumn(SessionRow row)
    {
        FpsReadoutModel m = FromRow(row);
        return m.Kind switch
        {
            // A steady-state factor carries its share in the grid too (rule 6; beta.8 — the column was the one place it did not).
            FpsReadoutKind.Generated when m.Factor is double f && m.SteadyShare is double share => string.Format(CultureInfo.CurrentCulture, "×{0:0.0} · {1:0}%", f, share * 100),
            FpsReadoutKind.Generated when m.Factor is double f => string.Format(CultureInfo.CurrentCulture, "×{0:0.0}", f),
            FpsReadoutKind.None => Strings.Common_Dash,
            _ => Strings.Common_NotAvailable,
        };
    }

    /// <summary>The tooltip that explains a column's negative or qualifier, or null when the figure needs none.</summary>
    public static string? ColumnTooltip(SessionRow row)
    {
        FpsReadoutModel m = FromRow(row);
        return m.Kind switch
        {
            FpsReadoutKind.Generated => m.QualifierTooltip ?? Strings.Fps_Native_Tooltip,
            FpsReadoutKind.None => Strings.Fps_None_Tooltip,
            FpsReadoutKind.Presented or FpsReadoutKind.IdentifiedUncounted => m.QualifierTooltip,
            _ => Strings.Tier_NA_Tooltip,
        };
    }

    /// <summary>
    /// The "Frame Generation" label a game page or the live card shows: the technology with its factor chip when
    /// counted, the technology with "factor not counted" when identified only, else the row's token as a name.
    /// </summary>
    public static string FrameGenerationLabel(FpsReadoutModel model, string? fgMode)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model.Kind switch
        {
            FpsReadoutKind.Generated when model.FactorChip is { } chip => Formats.FrameGeneration(fgMode) + " " + chip,
            FpsReadoutKind.IdentifiedUncounted => Formats.FrameGeneration(fgMode) + " · " + Strings.Fg_Factor_NotCounted,
            _ => Formats.FrameGeneration(fgMode),
        };
    }

    public static string GeneratedLine(double? native, double? displayed, double factor) => GeneratedLine(native, displayed, factor, steadyShare: null);

    /// <summary>
    /// <c>62 → 118 FPS (×1.9 FG)</c> — and a steady-state factor (CLAUDE.md rule 6, 2026-09-17) always with the share of the
    /// session it covers, <c>(×1.9 FG · 75%)</c>, wherever the line appears (beta.8: the Average card and Compare dropped it).
    /// </summary>
    public static string GeneratedLine(double? native, double? displayed, double factor, double? steadyShare) => steadyShare is double share
        ? string.Format(CultureInfo.CurrentCulture, Strings.Fps_Fg_Steady_Format, Formats.Fps(native), Formats.Fps(displayed), factor, share * 100)
        : string.Format(CultureInfo.CurrentCulture, Strings.Fps_Fg_Format, Formats.Fps(native), Formats.Fps(displayed), factor);

    public static string PresentedLine(double? presented) => string.Format(CultureInfo.CurrentCulture, Strings.Fps_Presented_Format, Formats.Fps(presented));

    public static string FactorChip(double factor) => FactorChip(factor, steadyShare: null);

    /// <summary>The factor chip; a steady-state factor always carries the share of the session it describes: <c>×2.0 FG · 75%</c>.</summary>
    public static string FactorChip(double factor, double? steadyShare) => steadyShare is double share
        ? string.Format(CultureInfo.CurrentCulture, Strings.Fps_Fg_Chip_Steady_Format, factor, share * 100)
        : string.Format(CultureInfo.CurrentCulture, Strings.Fps_Fg_Chip_Format, factor);

    /// <summary>
    /// The tooltip of a steady-state readout: why there is no session-wide factor, what the number is and over how much
    /// of the session, and the refusal's own numbers when the row stored them.
    /// </summary>
    public static string SteadyTooltip(double factor, double share, string? refusalDetail)
    {
        string text = string.Format(CultureInfo.CurrentCulture, Strings.Fps_Fg_Steady_Tooltip_Format, factor, share * 100);
        return RefusalDetailText(FgRefusalDetail.Parse(refusalDetail)) is { } numbers ? text + " " + numbers : text;
    }

    private static bool IsSteady(SessionRow row) => string.Equals(row.FgFactorScope, "steady", StringComparison.Ordinal);

    /// <summary>The census chip; a loaded frame-generation runtime is named when the census says which (<c>08_UI</c> §FPS display rule).</summary>
    public static string QualifierText(FpsQualifier qualifier, long? runtimeCensus = null) => qualifier switch
    {
        FpsQualifier.NoRuntime => Strings.Fps_Census_NoRuntime,
        FpsQualifier.RuntimeLoaded => FrameGenerationModule(runtimeCensus) is { } module
            ? string.Format(CultureInfo.CurrentCulture, Strings.Fps_Census_RuntimeLoaded_Named_Format, module)
            : Strings.Fps_Census_RuntimeLoaded,
        FpsQualifier.Withheld => Strings.Fps_Census_Withheld,
        _ => Strings.Fps_Census_NotRun,
    };

    public static string QualifierTooltip(FpsQualifier qualifier) => qualifier switch
    {
        FpsQualifier.NoRuntime => Strings.Fps_Census_NoRuntime_Tooltip,
        FpsQualifier.RuntimeLoaded => Strings.Fps_Census_RuntimeLoaded_Tooltip,
        FpsQualifier.Withheld => Strings.Fps_Census_Withheld_Tooltip,
        _ => Strings.Fps_Census_NotRun_Tooltip,
    };

    /// <summary>The chip for an identified, uncounted technology: <c>DLSS-G active — factor not counted</c>.</summary>
    public static string IdentifiedText(string? technology) =>
        string.Format(CultureInfo.CurrentCulture, Strings.Fps_Fg_Identified_Format, technology ?? Strings.Fg_Unknown);

    /// <summary>Its tooltip: the technology, the refusal in plain words, and why the number reads as Displayed.</summary>
    public static string IdentifiedTooltip(string? technology, string? refusal) => IdentifiedTooltip(technology, refusal, detail: null);

    /// <summary>
    /// The same tooltip with the refusal's numbers when the row stored them (<c>fg_refusal_detail</c>, schema 0005):
    /// "bucket 3 of 8 measured 2.25 against 3.65 for the whole session" says what "changed mid-session" cannot.
    /// </summary>
    public static string IdentifiedTooltip(string? technology, string? refusal, string? detail)
    {
        string text = string.Format(CultureInfo.CurrentCulture, Strings.Fps_Fg_Identified_Tooltip_Format, technology ?? Strings.Fg_Unknown, RefusalText(refusal));
        return RefusalDetailText(FgRefusalDetail.Parse(detail)) is { } numbers ? text + " " + numbers : text;
    }

    /// <summary>The sentence for a refusal's numbers, or null when the kind carries none worth a sentence (not counted, no evaluations, no batches).</summary>
    public static string? RefusalDetailText(FgRefusalDetail? detail)
    {
        if (detail is null)
        {
            return null;
        }

        CultureInfo culture = CultureInfo.CurrentCulture;
        return detail.Kind switch
        {
            "non_uniform" => string.Format(culture, Strings.Fg_RefusalDetail_NonUniform_Format,
                detail.BucketIndex + 1, detail.BucketCount, detail.BucketValue is double v ? Ratio(v) : Strings.Fg_RefusalDetail_NoTokens, Ratio(detail.Overall), detail.Count),
            "multiple_streams" => string.Format(culture, Strings.Fg_RefusalDetail_MultipleStreams_Format, detail.Count),
            "too_short" => string.Format(culture, Strings.Fg_RefusalDetail_TooShort_Format, detail.Count, FgWindow.MinSamplesToCheck),
            "ambiguous_band" => string.Format(culture, Strings.Fg_RefusalDetail_AmbiguousBand_Format, Ratio(detail.Overall), Ratio(FgWindow.NoneCeiling), Ratio(FgWindow.ActiveThreshold)),
            "unattributed" or "count_saturated" or "dxgi_saturated" => string.Format(culture, Strings.Fg_RefusalDetail_Records_Format, detail.Count),
            _ => null,
        };
    }

    private static string Ratio(double value) => value.ToString("0.00", CultureInfo.CurrentCulture);

    /// <summary>The row's <c>fg_refusal</c> token in the user's words; an unknown token is the honest "did not resolve".</summary>
    public static string RefusalText(string? refusal) => refusal switch
    {
        "not_counted" => Strings.Fg_Refusal_NotCounted,
        "unattributed" => Strings.Fg_Refusal_Unattributed,
        "multiple_streams" => Strings.Fg_Refusal_MultipleStreams,
        "count_saturated" => Strings.Fg_Refusal_CountSaturated,
        "dxgi_saturated" => Strings.Fg_Refusal_DxgiSaturated,
        "no_evaluations" => Strings.Fg_Refusal_NoEvaluations,
        "too_short" => Strings.Fg_Refusal_TooShort,
        "non_uniform" => Strings.Fg_Refusal_NonUniform,
        "ambiguous_band" => Strings.Fg_Refusal_AmbiguousBand,
        "no_batches" => Strings.Fg_Refusal_NoBatches,
        _ => Strings.Fg_Refusal_Unknown,
    };

    /// <summary>
    /// The frame-generation module the census saw, by the file names <c>fl_shm.h</c> §FlRuntimeCensus lists — the
    /// first family bit set, or null when the census carries none (or did not run). A name, not a measurement.
    /// </summary>
    public static string? FrameGenerationModule(long? runtimeCensus)
    {
        if (runtimeCensus is not { } raw)
        {
            return null;
        }

        var census = (FlRuntimeCensus)(uint)raw;
        if (census.HasFlag(FlRuntimeCensus.SlDlssG))
        {
            return "sl.dlss_g.dll";
        }

        if (census.HasFlag(FlRuntimeCensus.NvngxDlssG))
        {
            return "nvngx_dlssg.dll";
        }

        if (census.HasFlag(FlRuntimeCensus.LibXessFg))
        {
            return "libxess_fg.dll";
        }

        if (census.HasFlag(FlRuntimeCensus.FfxFrameInterpolation))
        {
            return "ffx_frameinterpolation_x64.dll";
        }

        if (census.HasFlag(FlRuntimeCensus.FfxFsr3))
        {
            return "ffx_fsr3_x64.dll";
        }

        if (census.HasFlag(FlRuntimeCensus.AmdFfxFrameGeneration))
        {
            return "amd_fidelityfx_framegeneration_dx12.dll";
        }

        if (census.HasFlag(FlRuntimeCensus.AmdFfxDx12))
        {
            return "amd_fidelityfx_dx12.dll";
        }

        return null;
    }

    /// <summary>The stored / wire token → the qualifier; an unknown token is "census not run", the honest unknown.</summary>
    public static FpsQualifier ParseQualifier(string? token) => token switch
    {
        "no_fg_runtime" => FpsQualifier.NoRuntime,
        "fg_runtime_loaded" => FpsQualifier.RuntimeLoaded,
        "none_withheld" => FpsQualifier.Withheld,
        _ => FpsQualifier.CensusNotRun,
    };

    private static FpsReadoutModel Shape(string? fgMode, double? native, double? displayed, double? factor, double? presented, FpsQualifier qualifier, string? refusal, long? runtimeCensus, string? refusalDetail = null, double? steadyShare = null)
    {
        // fg_mode: 'na' = not measured; 'none' = measured none; a technology with a factor = counted generation;
        // a technology WITHOUT a factor = identified, and the count refused (fg_refusal says why).
        if (string.IsNullOrEmpty(fgMode) || string.Equals(fgMode, "na", StringComparison.Ordinal))
        {
            return new FpsReadoutModel { Kind = FpsReadoutKind.Presented, Presented = presented, Qualifier = qualifier, RuntimeCensus = runtimeCensus };
        }

        if (string.Equals(fgMode, "none", StringComparison.Ordinal))
        {
            return new FpsReadoutModel { Kind = FpsReadoutKind.None, Native = native ?? presented };
        }

        if (factor is null)
        {
            return new FpsReadoutModel
            {
                Kind = FpsReadoutKind.IdentifiedUncounted,
                Presented = presented,
                Qualifier = qualifier,
                Technology = Formats.FrameGeneration(fgMode),
                Refusal = refusal,
                RefusalDetail = refusalDetail,
                RuntimeCensus = runtimeCensus,
            };
        }

        return new FpsReadoutModel { Kind = FpsReadoutKind.Generated, Native = native, Displayed = displayed, Factor = factor, SteadyShare = steadyShare, Refusal = refusal, RefusalDetail = refusalDetail };
    }
}
