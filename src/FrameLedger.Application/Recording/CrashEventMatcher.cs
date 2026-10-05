// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;

namespace FrameLedger.Application.Recording;

/// <summary>
/// Whether an Application-log record is a crash of THIS session's game (beta.14). Until then a record with id 1000 or 1001
/// counted when any of its strings CONTAINED the executable's name: Windows' memory-leak report (<c>RADAR_PRE_LEAK_64</c>, a
/// WER 1001 naming <c>witcher3.exe</c>) marked a session that ended with exit code 0 as crashed on the owner's machine, and so
/// would a hang report, another program's crash whose name contains this one's (<c>Game.exe</c> in <c>MyGame.exe</c>), or a
/// crash of a same-image child. Now:
/// <list type="bullet">
/// <item>1000 from <c>Application Error</c>: the faulting application's name equals the executable's, its path (when given)
/// is the executable's, and its process id (when the session held one) is the session's.</item>
/// <item>1001 from <c>Windows Error Reporting</c>: the report is a crash (<see cref="CrashReports"/>) and its first parameter
/// equals the executable's name.</item>
/// </list>
/// The indices are Windows' own templates (read on Windows 11 with <c>Get-WinEvent -ListProvider</c>): 1000 [0] AppName,
/// [8] ProcessId, [10] AppPath; 1001 [2] EventName, [5] P1. Pid and creation time are typed values, not strings.
/// </summary>
public static class CrashEventMatcher
{
    public const string ApplicationErrorProvider = "Application Error";

    public const string WerProvider = "Windows Error Reporting";

    /// <summary>The WER event names that are a crash; a hang (<c>AppHangB1</c>) or a leak report (<c>RADAR_PRE_LEAK_*</c>) is not.</summary>
    public static IReadOnlySet<string> CrashReports { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "APPCRASH", "BEX", "BEX64", "CLR20r3", "MoAppCrash", "MoBEX",
    };

    public static bool Matches(CrashLogRecord record, CrashQuery query)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(query);
        return record.EventId switch
        {
            1000 when string.Equals(record.Provider, ApplicationErrorProvider, StringComparison.OrdinalIgnoreCase) => ApplicationError(record.Properties, query),
            1001 when string.Equals(record.Provider, WerProvider, StringComparison.OrdinalIgnoreCase) => WerCrash(record.Properties, query),
            _ => false,
        };
    }

    private static bool ApplicationError(IReadOnlyList<object?> p, CrashQuery query)
    {
        if (!SameName(At(p, 0), query.ExeFileName))
        {
            return false;
        }

        if (At(p, 10) is string { Length: > 0 } appPath && !SamePath(appPath, query.ExePath))
        {
            return false;
        }

        return query.TargetPid == 0 || Number(At(p, 8)) is not { } pid || pid == (ulong)query.TargetPid;
    }

    private static bool WerCrash(IReadOnlyList<object?> p, CrashQuery query) =>
        At(p, 2) is string eventName && CrashReports.Contains(eventName) && SameName(At(p, 5), query.ExeFileName);

    private static object? At(IReadOnlyList<object?> p, int index) => index < p.Count ? p[index] : null;

    private static bool SameName(object? value, string exeFileName) =>
        value is string s && string.Equals(s.Trim(), exeFileName, StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a.Trim()), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>A number as the log gives it: an unsigned or signed integer, or text in decimal or <c>0x</c> hex.</summary>
    internal static ulong? Number(object? value) => value switch
    {
        ulong u => u,
        uint u => u,
        long l when l >= 0 => (ulong)l,
        int i when i >= 0 => (ulong)i,
        string s when s.Trim().StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && ulong.TryParse(s.Trim()[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong hex) => hex,
        string s when ulong.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong dec) => dec,
        _ => null,
    };
}
