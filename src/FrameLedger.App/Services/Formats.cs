using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Detection;
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
    public static string Fps(double? value) => value is double v ? FpsNumber(v) : Strings.Common_NotAvailable;

    public static string FpsOrDash(double? value) => value is double v ? FpsNumber(v) : Strings.Common_Dash;

    /// <summary>
    /// An FPS figure as <c>ui.fps_decimals</c> asks (beta.8): two decimals always ("62.40"), or a whole number. Rounded half
    /// away from zero — 62.5 is 63, as a reader expects; <see cref="Math.Round(double)"/> alone rounds half to even (62).
    /// </summary>
    public static string FpsNumber(double value) => FpsNumber(value, FpsDecimals.Two);

    /// <summary>The same with the choice given (tests, and anything that must not read the process-wide setting).</summary>
    public static string FpsNumber(double value, bool twoDecimals) => twoDecimals
        ? Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.CurrentCulture)
        : Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.CurrentCulture);

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

    /// <summary>
    /// The engine id as the engine's name (2026-09-23): a proper noun, the same in every language (the glossary rule
    /// above). The ids are the rules file's; one this table does not know — a newer rule, or an engine the user typed —
    /// passes through as it is, never blank.
    /// </summary>
    public static string Engine(string? id) => id switch
    {
        null => string.Empty,
        "unity" => "Unity",
        "unreal" => "Unreal Engine",
        "rpgmaker_mv" => "RPG Maker MV/MZ",
        "rpgmaker_rgss" => "RPG Maker XP/VX/VX Ace",
        "re_engine" => "RE Engine",
        "fromsoftware" => "FromSoftware",
        "godot" => "Godot",
        "gamemaker" => "GameMaker",
        "renpy" => "Ren'Py",
        "cryengine" => "CryEngine",
        "source" => "Source",
        _ => id,
    };

    /// <summary>What an executable runs as (<c>games.exe_machine</c>, beta.8), in the user's language.</summary>
    public static string Architecture(string? id) => id switch
    {
        ExecutableArchitecture.X64 => Strings.Arch_X64,
        ExecutableArchitecture.X86 => Strings.Arch_X86,
        ExecutableArchitecture.Arm64 => Strings.Arch_Arm64,
        ExecutableArchitecture.Arm => Strings.Arch_Arm,
        ExecutableArchitecture.AnyCpu => Strings.Arch_AnyCpu,
        ExecutableArchitecture.AnyCpu32 => Strings.Arch_AnyCpu32,
        ExecutableArchitecture.Other => Strings.Arch_Other,
        _ => Strings.Arch_Unknown,
    };

    /// <summary>
    /// <c>games.game_version</c> said as what it is (beta.8): a store import writes a Steam build id or a GOG / Epic
    /// version string, and a bare "20374416" in the subtitle said nothing. Labelled with the store when a store wrote it
    /// (<paramref name="detected"/>); a version the user typed is shown as typed. Null when there is none.
    /// </summary>
    public static string? StoreVersion(string? platform, string? version, bool detected)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        string? format = !detected ? null : platform switch
        {
            "steam" => Strings.Store_Steam_Format,
            "gog" => Strings.Store_Gog_Format,
            "epic" => Strings.Store_Epic_Format,
            _ => null,
        };
        return format is null ? version : string.Format(CultureInfo.CurrentCulture, format, version);
    }

    /// <summary>
    /// Whether two file versions are the same four numbers — Streamline stamps its version STRING as <c>2,8,0,0</c> and
    /// the fixed part reads <c>2.8.0.0</c>, which agree; a string with text after the numbers is read up to it. Null
    /// when either has no number to compare (beta.8: the session summary's "another copy was loaded").
    /// </summary>
    public static bool? SameVersion(string? a, string? b)
    {
        int[]? x = VersionNumbers(a);
        int[]? y = VersionNumbers(b);
        if (x is null || y is null)
        {
            return null;
        }

        for (int i = 0; i < 4; i++)
        {
            if ((i < x.Length ? x[i] : 0) != (i < y.Length ? y[i] : 0))
            {
                return false;
            }
        }

        return true;
    }

    private static int[]? VersionNumbers(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        List<int> parts = new(4);
        foreach (string token in text.Split(['.', ','], StringSplitOptions.TrimEntries))
        {
            // "3.7.10.0 built by …": the numbers end at the first token that is not all digits.
            string digits = new([.. token.TakeWhile(char.IsAsciiDigit)]);
            if (digits.Length == 0 || !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int n))
            {
                break;
            }

            parts.Add(n);
            if (parts.Count == 4 || digits.Length != token.Length)
            {
                break;
            }
        }

        return parts.Count == 0 ? null : [.. parts];
    }

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
                return string.Format(CultureInfo.CurrentCulture, Strings.Summary_Tier2_Why_Blocked_Format, BlockedReasonText.Describe(blocked));
            case "RefusedHookNotEnabled":
                return Strings.Summary_Tier2_Why_HookOff;
            case "RefusedByGuard" or "SafetyUnhook" when n.GuardFamily is { Length: > 0 } family:
                return string.Format(CultureInfo.CurrentCulture, Strings.Summary_Tier2_Why_Guard_Format, family, n.GuardSignal ?? Strings.Common_NotAvailable);
            case "RefusedByGuard" when n.GuardReason is { Length: > 0 } reason:
                // A refusal no family names (beta.8): the guard could not look, the game is 32-bit… — said as what it is,
                // never "… was detected in this game", which read as a finding.
                return GuardSentence(reason);
            case "TargetUnreadable":
                return Strings.Summary_Tier2_Why_Unreadable;
            case "RefusedConsentMissing":
                // Common after a store update (2026-09-23): the consent is about the executable that was enabled, and this is a newer one.
                return Strings.Summary_Tier2_Why_ConsentChanged;
            case "LaunchCannotStart" when n.LaunchError is { } error:
                return string.Format(CultureInfo.CurrentCulture, Strings.Launch_Failed_Format, Strings.End_LaunchCannotStart, LaunchErrorText(error));
            default:
                return EndReasonText(n.End) ?? string.Format(CultureInfo.CurrentCulture, Strings.Summary_Tier2_Why_Other_Format, n.End);
        }
    }

    /// <summary>
    /// Why a session ended, in words, for every <see cref="SessionEndReason"/> (beta.8); null for a name this build does not
    /// know. <c>Formats_EveryEndReasonHasItsOwnSentence</c> walks the enum so none falls through.
    /// </summary>
    public static string? EndReasonText(string? end) => end switch
    {
        nameof(SessionEndReason.Running) => Strings.End_Running,
        nameof(SessionEndReason.TargetExited) => Strings.End_TargetExited,
        nameof(SessionEndReason.SafetyUnhook) => Strings.End_SafetyUnhook,
        nameof(SessionEndReason.SupervisionLost) => Strings.End_SupervisionLost,
        nameof(SessionEndReason.WriterSelfDisabled) => Strings.End_WriterSelfDisabled,
        nameof(SessionEndReason.WriterStoppedBlocklisted) => Strings.End_WriterStoppedBlocklisted,
        nameof(SessionEndReason.WriterNeverInstalledHooks) => Strings.End_WriterNeverInstalledHooks,
        nameof(SessionEndReason.RefusedHookNotEnabled) => Strings.End_RefusedHookNotEnabled,
        nameof(SessionEndReason.RefusedConsentMissing) => Strings.End_RefusedConsentMissing,
        nameof(SessionEndReason.RefusedPreviouslyBlocked) => Strings.End_RefusedPreviouslyBlocked,
        nameof(SessionEndReason.RefusedByGuard) => Strings.End_RefusedByGuard,
        nameof(SessionEndReason.PreScanCouldNotVerify) => Strings.End_PreScanCouldNotVerify,
        nameof(SessionEndReason.TargetNotRunning) => Strings.End_TargetNotRunning,
        nameof(SessionEndReason.TargetCannotBePinned) => Strings.End_TargetCannotBePinned,
        nameof(SessionEndReason.ExecutableUnreadable) => Strings.End_ExecutableUnreadable,
        nameof(SessionEndReason.SupervisionFaulted) => Strings.End_SupervisionFaulted,
        nameof(SessionEndReason.TargetAmbiguous) => Strings.End_TargetAmbiguous,
        nameof(SessionEndReason.AttachRefused) => Strings.End_AttachRefused,
        nameof(SessionEndReason.LaunchCannotStart) => Strings.End_LaunchCannotStart,
        nameof(SessionEndReason.LaunchTargetExited) => Strings.End_LaunchTargetExited,
        nameof(SessionEndReason.LaunchNoPresentationRuntime) => Strings.End_LaunchNoPresentationRuntime,
        nameof(SessionEndReason.RefusedKillSwitch) => Strings.End_RefusedKillSwitch,
        nameof(SessionEndReason.KillSwitchEngaged) => Strings.End_KillSwitchEngaged,
        nameof(SessionEndReason.StoppedByUser) => Strings.End_StoppedByUser,
        nameof(SessionEndReason.TargetUnreadable) => Strings.End_TargetUnreadable,
        _ => null,
    };

    /// <summary>
    /// A guard refusal no anti-cheat family names, as a sentence (beta.8): "The anti-cheat guard did not hook this game: the
    /// game is 32-bit…". Every refusal reason the native guard can give has its words; a name this build does not know is
    /// named as it is.
    /// </summary>
    public static string GuardSentence(string reason) =>
        string.Format(CultureInfo.CurrentCulture, Strings.Guard_Refused_Format, GuardReasonText(reason));

    /// <summary>What a familyless guard reason means, as a clause.</summary>
    public static string GuardReasonText(string reason) => reason switch
    {
        "ModuleScanFailed" => Strings.Guard_ModuleScanFailed,
        "ProcessUnreadable" => Strings.Guard_ProcessUnreadable,
        "ProcessTreeUnavailable" => Strings.Guard_ProcessTreeUnavailable,
        "DriverScanFailed" => Strings.Guard_DriverScanFailed,
        "ServiceQueryFailed" => Strings.Guard_ServiceQueryFailed,
        "RulesUnreadable" => Strings.Guard_RulesUnreadable,
        "RulesMalformed" => Strings.Guard_RulesMalformed,
        "RulesIncomplete" => Strings.Guard_RulesIncomplete,
        "PreScanFailed" => Strings.Guard_PreScanFailed,
        "InjectionFailed" => Strings.Guard_InjectionFailed,
        "TargetIsWow64" => Strings.Guard_TargetIsWow64,
        "PayloadNotOurs" => Strings.Guard_PayloadNotOurs,
        "HookNotEnabled" => Strings.Guard_HookNotEnabled,
        "ConsentMissing" => Strings.Guard_ConsentMissing,
        "PreviouslyBlocked" => Strings.Guard_PreviouslyBlocked,
        "LaunchTargetExited" => Strings.Guard_LaunchTargetExited,
        "LaunchNoPresentationRuntime" => Strings.Guard_LaunchNoPresentationRuntime,
        "TargetIsVulkanLayered" => Strings.Guard_TargetIsVulkanLayered,
        "SuspiciousUnsigned" => Strings.Guard_SuspiciousUnsigned,
        _ => string.Format(CultureInfo.CurrentCulture, Strings.Guard_Other_Format, reason),
    };

    /// <summary>
    /// How a session ended, in words (beta.8): a crash names its exception ("crashed (0xC0000005 access violation)"), an
    /// exit code that is not one is an ordinary end with its code — End task's 1 said as what it is — and the other
    /// statuses keep their words.
    /// </summary>
    public static string ExitText(ExitStatus status, CaptureNotes notes) => status switch
    {
        ExitStatus.Crashed => notes.ExitCode is { } code && ExitStatusMapper.IsExceptionCode(code)
            ? string.Format(CultureInfo.CurrentCulture, Strings.Exit_Crashed_Format, ExceptionName(code))
            : Strings.Exit_CrashEvent,
        ExitStatus.Normal => notes.ExitCode switch
        {
            null or 0 => Strings.Exit_EndedNormally,
            1 => Strings.Exit_Code1,
            { } other => string.Format(CultureInfo.CurrentCulture, Strings.Exit_Code_Format, CodeText(other)),
        },
        _ => ExitStatusText(status),
    };

    /// <summary>The exception an exit code names, or the code in hex.</summary>
    public static string ExceptionName(int code) => unchecked((uint)code) switch
    {
        0xC0000005 => Strings.Exit_Ex_AccessViolation,
        0xC0000006 => Strings.Exit_Ex_InPageError,
        0xC00000FD => Strings.Exit_Ex_StackOverflow,
        0xC0000374 => Strings.Exit_Ex_HeapCorruption,
        0xC0000409 => Strings.Exit_Ex_FailFast,
        0xC0000142 => Strings.Exit_Ex_DllInit,
        0xC0000135 => Strings.Exit_Ex_DllNotFound,
        0xC000007B => Strings.Exit_Ex_BadImage,
        0xE06D7363 => Strings.Exit_Ex_Cpp,
        0xE0434352 => Strings.Exit_Ex_Clr,
        0x80000003 => Strings.Exit_Ex_Breakpoint,
        _ => CodeText(code),
    };

    /// <summary>An exit code as a person reads it: small ones in decimal, NTSTATUS-shaped ones in hex.</summary>
    private static string CodeText(int code) =>
        code is >= -255 and <= 65535 ? code.ToString(CultureInfo.CurrentCulture) : "0x" + unchecked((uint)code).ToString("X8", CultureInfo.InvariantCulture);

    /// <summary>A launch's Win32 error in words (beta.8): the few a game launch meets, and the number otherwise.</summary>
    public static string LaunchErrorText(int error) => error switch
    {
        2 => Strings.Launch_Error_2,
        3 => Strings.Launch_Error_3,
        5 => Strings.Launch_Error_5,
        193 => Strings.Launch_Error_193,
        740 => Strings.Launch_Error_740,
        _ => string.Format(CultureInfo.CurrentCulture, Strings.Launch_Error_Format, error),
    };

    /// <summary>
    /// Why an executable is not where the row says (beta.8): a drive that is not connected — the owner's external drive
    /// changes letter, 2026-09-22 — is said as that, and FrameLedger finds the game again when it is back; otherwise the
    /// file itself is gone from its path.
    /// </summary>
    public static string ExecutableMissingText(string exePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        string? root = Path.GetPathRoot(exePath);
        return !string.IsNullOrEmpty(root) && !Directory.Exists(root)
            ? string.Format(CultureInfo.CurrentCulture, Strings.Exe_DriveMissing_Format, root)
            : string.Format(CultureInfo.CurrentCulture, Strings.Exe_FileMissing_Format, exePath);
    }
}
