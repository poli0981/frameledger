// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using FrameLedger.Agent.Composition;

namespace FrameLedger.Agent.Tests;

/// <summary>
/// beta.14 (D50): Task Manager lists a process by its file description, and until beta.14 the Agent's read
/// "FrameLedger.Agent" — the SDK's default, the assembly name, with the same company and product and no copyright. The
/// Agent is the process that injects; it says plainly what it is and who wrote it, as the native binaries always have.
/// </summary>
public sealed class AssemblyIdentityTests
{
    private static string RepoRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FrameLedger.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("the repository root (FrameLedger.slnx) is not above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void TheAgentNamesItselfAndCarriesTheNativeCopyright()
    {
        Assembly agent = typeof(AgentIdentityFactory).Assembly;
        FileVersionInfo block = FileVersionInfo.GetVersionInfo(agent.Location);
        Match native = Regex.Match(File.ReadAllText(Path.Combine(RepoRoot(), "src", "native", "CMakeLists.txt")),
            "^set\\(FL_COPYRIGHT\\s+\"(?<text>[^\"]+)\"\\)", RegexOptions.Multiline | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        (block.FileDescription, block.CompanyName, block.ProductName).Should().Be(("FrameLedger Agent", "FrameLedger", "FrameLedger"),
            "the apphost FrameLedger.Agent.exe carries a copy of this block, and Task Manager shows its description");
        native.Success.Should().BeTrue("src/native/CMakeLists.txt sets FL_COPYRIGHT");
        block.LegalCopyright.Should().Be(native.Groups["text"].Value, "one product, one copyright");
        (agent.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description).Should().NotBeNullOrWhiteSpace();
    }
}
