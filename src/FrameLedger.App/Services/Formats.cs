using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.Services;

/// <summary>
/// Culture-aware text for the numbers the pages show (<c>09_I18N</c> §Formatting rules: numbers and dates through
/// the current culture; charts and exports are invariant and live elsewhere). Product names (DLSS, FSR, XeSS,
/// NIS) are proper nouns and stay as they are in every language, per the glossary.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
public static class Formats
{
    public static string Fps(double? value) => value is double v ? Math.Round(v).ToString("0", CultureInfo.CurrentCulture) : Strings.Common_NotAvailable;

    public static string FpsOrDash(double? value) => value is double v ? Math.Round(v).ToString("0", CultureInfo.CurrentCulture) : Strings.Common_Dash;

    /// <summary><c>1h 23m</c> above an hour, <c>4m 05s</c> below.</summary>
    public static string Duration(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.TotalHours >= 1
            ? string.Format(CultureInfo.CurrentCulture, Strings.Format_Duration_HoursMinutes_Format, (int)span.TotalHours, span.Minutes)
            : string.Format(CultureInfo.CurrentCulture, Strings.Format_Duration_MinutesSeconds_Format, span.Minutes, span.Seconds);
    }

    /// <summary>Playtime in hours to one decimal, or minutes under an hour.</summary>
    public static string Playtime(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.TotalHours >= 1
            ? string.Format(CultureInfo.CurrentCulture, Strings.Format_Playtime_Hours_Format, span.TotalHours)
            : string.Format(CultureInfo.CurrentCulture, Strings.Format_Playtime_Minutes_Format, (int)span.TotalMinutes);
    }

    public static string Date(DateTimeOffset at) => at.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public static string Temperature(double? celsius) => celsius is double c ? string.Format(CultureInfo.CurrentCulture, Strings.Format_Temperature_Format, c) : Strings.Common_NotAvailable;

    /// <summary><c>1485×835 → 2560×1440</c>, or one pair when only one is known, or N/A.</summary>
    public static string Resolution(int? renderW, int? renderH, int? outputW, int? outputH)
    {
        bool render = renderW is > 0 && renderH is > 0;
        bool output = outputW is > 0 && outputH is > 0;
        if (render && output && (renderW != outputW || renderH != outputH))
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.Format_Resolution_Format, renderW, renderH, outputW, outputH);
        }

        return output
            ? string.Format(CultureInfo.CurrentCulture, Strings.Format_Resolution_Single_Format, outputW, outputH)
            : render
                ? string.Format(CultureInfo.CurrentCulture, Strings.Format_Resolution_Single_Format, renderW, renderH)
                : Strings.Common_NotAvailable;
    }

    /// <summary>The row's upscaler token as its product name; null → N/A; <c>none</c> → the resource.</summary>
    public static string Upscaler(string? token) => token switch
    {
        null or "" => Strings.Common_NotAvailable,
        "dlss" => "DLSS",
        "fsr" => "FSR",
        "fsr2" => "FSR 2",
        "fsr3" => "FSR 3",
        "fsr4" => "FSR 4",
        "xess" => "XeSS",
        "nis" => "NIS",
        "none" => Strings.Upscaler_None,
        _ => Strings.Upscaler_Unknown,
    };

    /// <summary>The row's fg_mode token as its product name.</summary>
    public static string FrameGeneration(string? token) => token switch
    {
        null or "" or "na" => Strings.Common_NotAvailable,
        "dlssg" => "DLSS-G",
        "fsrfg" => "FSR FG",
        "xefg" => "XeFG",
        "none" => Strings.Fg_None,
        "active" => Strings.Fg_Active,
        _ => Strings.Fg_Unknown,
    };

    /// <summary>The API token as its name (DXGI's own spelling).</summary>
    public static string Api(string? token) => token switch
    {
        "d3d11" => "D3D11",
        "d3d12" => "D3D12",
        "vulkan" => "Vulkan",
        "opengl" => "OpenGL",
        _ => Strings.Common_NotAvailable,
    };

    public static string ExitStatusText(ExitStatus status) => status switch
    {
        ExitStatus.Crashed => Strings.Exit_Crashed,
        ExitStatus.UnhookedSafety => Strings.Exit_UnhookedSafety,
        ExitStatus.Degraded => Strings.Exit_Degraded,
        ExitStatus.Interrupted => Strings.Exit_Interrupted,
        _ => Strings.Exit_Normal,
    };

    /// <summary>The platform token as its store name.</summary>
    public static string Platform(string? token) => token switch
    {
        "steam" => "Steam",
        "gog" => "GOG",
        "epic" => "Epic Games",
        "itch" => "itch.io",
        _ => string.Empty,
    };
}
