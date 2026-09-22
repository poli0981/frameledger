using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FrameLedger.Domain.Metrics;
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

    /// <summary>A 0–100 load as <c>42%</c>; null → N/A, never 0%.</summary>
    public static string Percent(double? value) => value is double v ? v.ToString("0", CultureInfo.CurrentCulture) + "%" : Strings.Common_NotAvailable;

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

    /// <summary>
    /// The upscaler as the UI states it, down <c>03_METRICS</c> §Upscaling's ladder: a name a HOOK produced outranks
    /// everything and takes its measured preset; where no hook named one (<c>unknown</c>: a hook ran and saw nothing —
    /// every NGX-direct DLSS title, since no NGX hook may exist — or no claim at all) and the NVIDIA driver reports the
    /// feature created and evaluated in the process, the name is the driver's and SAYS so; otherwise what the token
    /// says. The driver's word is identity only: it never takes a quality, even when a byte is on the row.
    /// </summary>
    /// <remarks>
    /// The row has stored <c>upscaler_driver_reported</c> since P2 and the capture host has printed it since 2026-09-06;
    /// the App never read it, which is why DLSS read "Unknown upscaler" on most titles while FSR, whose dispatch the
    /// hook does see, was named (owner's report, 2026-09-21).
    /// </remarks>
    public static string Upscaler(string? token, string? quality, string? driverReported)
    {
        bool hookNamed = token is { Length: > 0 } and not "unknown";
        if (!hookNamed && driverReported is { Length: > 0 })
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.Upscaler_DriverReported_Format, Upscaler(driverReported));
        }

        string name = Upscaler(token);
        return hookNamed && UpscalerNames.Quality(token, quality) is { } preset ? name + " " + preset : name;
    }

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

    /// <summary>
    /// Why a Tier-2 session measured nothing, from <c>capture_notes</c> (2026-09-22): hooking off, a block on the row,
    /// the guard's refusal by family, a process that could not be opened, or the end reason by name.
    /// </summary>
    public static string Tier2Reason(string? captureNotes) => Tier2Reason(Application.Recording.CaptureNotes.Parse(captureNotes));

    /// <summary>The same words for a session still running (2026-09-23): the hold its <c>SessionStarted</c> carried.</summary>
    public static string Tier2HoldReason(Shared.Ipc.SessionHold hold)
    {
        ArgumentNullException.ThrowIfNull(hold);
        return Tier2Reason(new Application.Recording.CaptureNotes(hold.Reason, hold.GuardReason, hold.Family, hold.Signal));
    }

    private static string Tier2Reason(Application.Recording.CaptureNotes n)
    {
        switch (n.End)
        {
            case null:
                return string.Empty;
            case "RefusedHookNotEnabled" when string.Equals(n.GuardReason, "PreviouslyBlocked", StringComparison.Ordinal) && n.GuardSignal is { Length: > 0 } blocked:
                return string.Format(CultureInfo.CurrentCulture, Strings.Summary_Tier2_Why_Blocked_Format, blocked);
            case "RefusedHookNotEnabled":
                return Strings.Summary_Tier2_Why_HookOff;
            case "RefusedByGuard" or "SafetyUnhook" when n.GuardFamily is { Length: > 0 } family:
                return string.Format(CultureInfo.CurrentCulture, Strings.Summary_Tier2_Why_Guard_Format, family, n.GuardSignal ?? Strings.Common_NotAvailable);
            case "TargetUnreadable":
                return Strings.Summary_Tier2_Why_Unreadable;
            case "RefusedConsentMissing":
                // Common after a store update (2026-09-23): the consent is about the executable that was enabled, and this is a newer one.
                return Strings.Summary_Tier2_Why_ConsentChanged;
            default:
                return string.Format(CultureInfo.CurrentCulture, Strings.Summary_Tier2_Why_Other_Format, n.End);
        }
    }
}
