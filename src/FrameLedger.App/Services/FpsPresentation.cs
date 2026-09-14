using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.Application.Persistence;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Services;

/// <summary>
/// CLAUDE.md rule 6 / <c>08_UI</c> §FPS display rule, decided in ONE place: whether a row (or a live progress
/// event) shows <c>62 → 118 FPS (×1.9 FG)</c>, <c>144 FPS</c> alone, or Presented FPS with its census qualifier —
/// and what a table's Native / Displayed / FG× columns say, where <c>—</c> (measured none) and <c>N/A</c> (not
/// measured) are two different negatives that must not collapse.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
public static class FpsPresentation
{
    /// <summary>The row's shape: generated when a frame-generation mode was measured, none when measured none, presented otherwise.</summary>
    public static FpsReadoutModel FromRow(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Tier != Domain.Sessions.CaptureTier.Hooked || row.FrameCount == 0)
        {
            return FpsReadoutModel.Unavailable;
        }

        return Shape(row.FgMode, row.NativeFps, row.DisplayedFps, row.FgFactor, row.PresentedFps ?? row.NativeFps, ParseQualifier(row.PresentedQualifier));
    }

    /// <summary>The live card's shape from a 1 Hz progress event (<c>07_IPC</c>: the FG fields are set only when measured).</summary>
    public static FpsReadoutModel FromProgress(SessionProgressEvent progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (progress.Presents5s == 0)
        {
            return FpsReadoutModel.Unavailable;
        }

        return Shape(progress.FgMode, progress.NativeFps5s, progress.DisplayedFps5s, progress.FgFactor, progress.PresentedFps5s ?? progress.NativeFps5s, ParseQualifier(progress.PresentedQualifier));
    }

    /// <summary>The table's Native column: the native figure when FG was measured, else the Presented figure (its qualifier is the tooltip).</summary>
    public static string NativeColumn(SessionRow row) => FromRow(row).PrimaryText;

    /// <summary>The table's Displayed column: the figure when FG was measured, <c>—</c> when measured none, <c>N/A</c> when not measured.</summary>
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

    /// <summary>The table's FG× column, on the same rule as <see cref="DisplayedColumn"/>.</summary>
    public static string FactorColumn(SessionRow row)
    {
        FpsReadoutModel m = FromRow(row);
        return m.Kind switch
        {
            FpsReadoutKind.Generated => m.Factor is double f ? string.Format(CultureInfo.CurrentCulture, "×{0:0.0}", f) : Strings.Common_NotAvailable,
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
            FpsReadoutKind.Generated => Strings.Fps_Native_Tooltip,
            FpsReadoutKind.None => Strings.Fps_None_Tooltip,
            FpsReadoutKind.Presented => m.QualifierTooltip,
            _ => Strings.Tier_NA_Tooltip,
        };
    }

    public static string GeneratedLine(double? native, double? displayed, double? factor) =>
        string.Format(CultureInfo.CurrentCulture, Strings.Fps_Fg_Format, Formats.Fps(native), Formats.Fps(displayed), factor ?? 0);

    public static string PresentedLine(double? presented) => string.Format(CultureInfo.CurrentCulture, Strings.Fps_Presented_Format, Formats.Fps(presented));

    public static string FactorChip(double factor) => string.Format(CultureInfo.CurrentCulture, Strings.Fps_Fg_Chip_Format, factor);

    public static string QualifierText(FpsQualifier qualifier) => qualifier switch
    {
        FpsQualifier.NoRuntime => Strings.Fps_Census_NoRuntime,
        FpsQualifier.RuntimeLoaded => Strings.Fps_Census_RuntimeLoaded,
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

    /// <summary>The stored / wire token → the qualifier; an unknown token is "census not run", the honest unknown.</summary>
    public static FpsQualifier ParseQualifier(string? token) => token switch
    {
        "no_fg_runtime" => FpsQualifier.NoRuntime,
        "fg_runtime_loaded" => FpsQualifier.RuntimeLoaded,
        "none_withheld" => FpsQualifier.Withheld,
        _ => FpsQualifier.CensusNotRun,
    };

    private static FpsReadoutModel Shape(string? fgMode, double? native, double? displayed, double? factor, double? presented, FpsQualifier qualifier)
    {
        // fg_mode: 'na' = not measured; 'none' = measured none; anything else = measured generation.
        if (string.IsNullOrEmpty(fgMode) || string.Equals(fgMode, "na", StringComparison.Ordinal))
        {
            return new FpsReadoutModel { Kind = FpsReadoutKind.Presented, Presented = presented, Qualifier = qualifier };
        }

        if (string.Equals(fgMode, "none", StringComparison.Ordinal))
        {
            return new FpsReadoutModel { Kind = FpsReadoutKind.None, Native = native ?? presented };
        }

        return new FpsReadoutModel { Kind = FpsReadoutKind.Generated, Native = native, Displayed = displayed, Factor = factor };
    }
}
