// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using System.Reflection;
using FluentAssertions;
using FrameLedger.Shared;

namespace FrameLedger.Infrastructure.Tests;

/// <summary>
/// D73: the release line is the <c>RELEASE_LINE</c> file, stamped into the assemblies by <c>Directory.Build.props</c> and
/// read back by <see cref="BuildLine"/>. On <c>main</c> the file says <c>main</c> and on the <c>alpha</c> branch
/// <c>alpha</c>, so these tests hold on both: what they pin is that the three agree, not which line this is.
/// </summary>
public sealed class BuildLineTests
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

    [Theory]
    [InlineData("main", false)]
    [InlineData("alpha", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("Alpha", false)]
    [InlineData("beta", false)]
    public void OnlyTheExactWordAlphaIsTheAlphaLine(string? stamped, bool alpha)
    {
        BuildLine line = BuildLine.Parse(stamped);

        line.IsAlpha.Should().Be(alpha);
        line.Should().BeSameAs(alpha ? BuildLine.Alpha : BuildLine.Main);
    }

    [Fact]
    public void TheBuildCarriesTheLineTheRepositoryNames()
    {
        string file = File.ReadAllText(Path.Combine(RepoRoot(), "RELEASE_LINE")).Trim();
        string? stamped = typeof(BuildLine).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(static a => string.Equals(a.Key, BuildLine.MetadataKey, StringComparison.Ordinal))?.Value;

        file.Should().BeOneOf("main", "alpha");
        stamped.Should().Be(file, "Directory.Build.props stamps RELEASE_LINE into every assembly");
        BuildLine.Current.Name.Should().Be(file);
    }
}
