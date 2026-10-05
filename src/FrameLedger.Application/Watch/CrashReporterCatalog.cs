// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Watch;

/// <summary>
/// The crash reporters engines and crash SDKs start (beta.14), by exact image name. Two kinds, and the difference is the
/// whole point: an <b>on-crash</b> reporter is started BY the crash — Unreal's <c>CrashReportClient.exe</c>, REDengine's
/// <c>CrashReporter.exe</c>, RE Engine's <c>CrashReport.exe</c>, BugSplat's <c>BsSndRpt64.exe</c> — so its start beside a
/// game that then exits is a witness; an <b>always-on</b> handler starts WITH the game and lives beside it — Unity's
/// <c>UnityCrashHandler64.exe</c>, Chromium's and Sentry's <c>crashpad_handler.exe</c> — so its presence says nothing and it
/// never counts. All six were found in the owner's Steam libraries (2026-10-05).
/// </summary>
public static class CrashReporterCatalog
{
    public static IReadOnlySet<string> OnCrash { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CrashReportClient.exe", "CrashReporter.exe", "CrashReport.exe", "BsSndRpt.exe", "BsSndRpt64.exe",
    };

    public static IReadOnlySet<string> AlwaysOn { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "UnityCrashHandler64.exe", "UnityCrashHandler32.exe", "crashpad_handler.exe",
    };

    public static bool IsOnCrash(string? imageName) => imageName is not null && OnCrash.Contains(imageName);
}
