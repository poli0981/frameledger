// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;

namespace FrameLedger.Infrastructure.Tests;

/// <summary>
/// Every file in <c>src</c> that starts a process — or, the logon task, has Windows start one — by name (2026-11-11,
/// <c>docs/13_CI_CD.md</c> §Qodana). Qodana's rule for it — JetBrains' <c>csharp_injection_rule-CommandInjection</c>,
/// any <c>Process.Start</c> whose argument is not a literal — reported ten of these; each was read, and its file is
/// listed in <c>qodana.yaml</c>. That rule is a pattern in an advisory job. This sweep is the same tripwire in the
/// gate, and wider: it also sees <c>CreateProcessWithTokenW</c> and the logon task. A new file that starts a process
/// fails here until someone has read what it starts and where the path and the arguments come from, and added it to
/// the list — and to <c>qodana.yaml</c> and the table in that section, where each of these is described.
/// </summary>
public sealed class ProcessStartSitesTests
{
    /// <summary>Read on 2026-11-11: what each starts, and from where, is the table in <c>13_CI_CD</c> §Qodana.</summary>
    private static readonly string[] _read =
    [
        "FrameLedger.App/Services/AgentLauncher.cs",
        "FrameLedger.App/Services/AgentTool.cs",
        "FrameLedger.App/Services/Explorer.cs",
        "FrameLedger.App/Services/ShellStarter.cs",
        "FrameLedger.App/Services/ShellUrlOpener.cs",
        "FrameLedger.App/ViewModels/SettingsViewModel.cs",
        "FrameLedger.Infrastructure/Capture/ProcessLauncher.cs",
        "FrameLedger.Infrastructure/Diagnostics/CrashDumpWriter.cs",
        "FrameLedger.Infrastructure/Startup/AgentElevation.cs",
        "FrameLedger.Infrastructure/Startup/LogonTask.cs",
        "FrameLedger.Infrastructure/Startup/UnelevatedProcess.cs",
    ];

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "FrameLedger.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("FrameLedger.slnx not found above the test binary");
    }

    private static bool StartsAProcess(string file)
    {
        string text = File.ReadAllText(file);
        return text.Contains("Process.Start(", StringComparison.Ordinal)
            || text.Contains("new ProcessStartInfo", StringComparison.Ordinal)
            || text.Contains("PInvoke.CreateProcess", StringComparison.Ordinal)
            || text.Contains("RegisterTaskDefinition(", StringComparison.Ordinal);
    }

    [Fact]
    public void EveryFileThatStartsAProcessIsOneThatWasRead()
    {
        string src = Path.Combine(RepoRoot(), "src");
        string obj = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
        string bin = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
        string[] found = [.. Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(obj, StringComparison.Ordinal) && !f.Contains(bin, StringComparison.Ordinal))
            .Where(StartsAProcess)
            .Select(f => Path.GetRelativePath(src, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];

        found.Should().Equal(_read,
            "a file that starts a process is read first - what it starts, and where the path and the arguments come from - and then listed here and in docs/13_CI_CD.md §Qodana");
    }
}
