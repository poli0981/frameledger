// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using FluentAssertions.Execution;
using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.14 (D50): every managed assembly the package ships names the product, says what it is and carries the copyright
/// the native binaries carry. Until beta.14 the SDK's defaults stood — each library's company and product were its own
/// assembly name, the Agent's "FrameLedger.Agent" — and no managed binary had a copyright; Explorer's Properties ▸
/// Details and Task Manager read these. <c>tools/versioninfo-check.ps1</c> reads the same from the built and the
/// published binaries; this pins the source, against the native <c>FL_COPYRIGHT</c> word for word.
/// </summary>
public sealed class AssemblyIdentityTests
{
    private static readonly Assembly[] _shipped =
    [
        typeof(UiIdentity).Assembly,
        typeof(Domain.Sessions.ExitStatus).Assembly,
        typeof(Application.Recording.ExitStatusMapper).Assembly,
        typeof(Infrastructure.Persistence.LedgerPaths).Assembly,
        typeof(Shared.Ipc.IpcProtocol).Assembly,
    ];

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

    /// <summary>The native version blocks' copyright line, as <c>src/native/CMakeLists.txt</c> sets it.</summary>
    private static string NativeCopyright()
    {
        string cmake = File.ReadAllText(Path.Combine(RepoRoot(), "src", "native", "CMakeLists.txt"));
        Match line = Regex.Match(cmake, "^set\\(FL_COPYRIGHT\\s+\"(?<text>[^\"]+)\"\\)", RegexOptions.Multiline | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        line.Success.Should().BeTrue("src/native/CMakeLists.txt sets FL_COPYRIGHT");
        return line.Groups["text"].Value;
    }

    [Fact]
    public void EveryShippedAssemblyNamesTheProductAndCarriesTheNativeCopyright()
    {
        string copyright = NativeCopyright();

        using var scope = new AssertionScope();
        foreach (Assembly assembly in _shipped)
        {
            string name = assembly.GetName().Name!;
            (assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company).Should().Be("FrameLedger", name);
            (assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product).Should().Be("FrameLedger", name);
            (assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright).Should().Be(copyright, name);
            (assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description).Should().NotBeNullOrWhiteSpace(name);
            // The repository every assembly names is the one the App sends issues and document links to.
            (assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(static a => string.Equals(a.Key, "RepositoryUrl", StringComparison.Ordinal))?.Value)
                .Should().Be(IssueLink.Repository, name);
        }
    }

    [Fact]
    public void TheAppsVersionBlockIsWhatExplorerShows()
    {
        Assembly app = typeof(UiIdentity).Assembly;
        FileVersionInfo block = FileVersionInfo.GetVersionInfo(app.Location);

        (block.FileDescription, block.CompanyName, block.ProductName).Should().Be(("FrameLedger", "FrameLedger", "FrameLedger"),
            "the apphost FrameLedger.exe carries a copy of this block");
        block.LegalCopyright.Should().Be(NativeCopyright(), "one product, one copyright");
    }
}
