// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Reflection;
using FluentAssertions;
using FrameLedger.Shared;

namespace FrameLedger.Infrastructure.Tests;

/// <summary>
/// D97 and D98 (2026-11-11): what an alpha says about its .NET follows the runtime's own description, and that its line is
/// paused follows a stamp <c>Directory.Build.props</c> writes. Like <see cref="BuildLineTests"/>, the stamp's test holds on
/// both branches: it pins that the build carries what the file says, not which value that is.
/// </summary>
public sealed class AlphaLineTests
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
    [InlineData(".NET 11.0.0", false)]
    [InlineData(".NET 11.0.1", false)]
    [InlineData(".NET 10.0.15", false)]
    [InlineData(".NET 11.0.0-rc.3.26522.148", true)]
    [InlineData(".NET 12.0.0-preview.1.27101.5", true)]
    [InlineData(".NET 11.0.0-rc.3.26522.148 ", true)]
    [InlineData("11.0.0", false)]
    [InlineData("", false)]
    public void ARuntimeIsAPrereleaseWhenItsVersionCarriesAHyphen(string description, bool prerelease) =>
        AlphaLine.IsPrerelease(description).Should().Be(prerelease);

    [Fact]
    public void AHyphenBeforeTheVersionIsNotThePrereleaseMark() =>
        AlphaLine.IsPrerelease("Some-Runtime 11.0.0").Should().BeFalse("the version is the last word, and it has no hyphen");

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("yes", false)]
    public void OnlyTheWordTrueSaysTheLineRests(string? stamped, bool rests) => AlphaLine.ParseRests(stamped).Should().Be(rests);

    [Fact]
    public void TheBuildCarriesWhatDirectoryBuildPropsSaysAboutTheAlphaLine()
    {
        const string open = "<FlAlphaLineRests>";
        const string close = "</FlAlphaLineRests>";
        string props = File.ReadAllText(Path.Combine(RepoRoot(), "Directory.Build.props"));
        int from = props.IndexOf(open, StringComparison.Ordinal);
        int to = props.IndexOf(close, StringComparison.Ordinal);
        string? stamped = typeof(AlphaLine).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(static a => string.Equals(a.Key, AlphaLine.RestsMetadataKey, StringComparison.Ordinal))?.Value;

        from.Should().BeGreaterThanOrEqualTo(0, "Directory.Build.props declares FlAlphaLineRests");
        to.Should().BeGreaterThan(from);
        string declared = props[(from + open.Length)..to];
        declared.Should().BeOneOf("true", "false");
        stamped.Should().Be(declared, "Directory.Build.props stamps the property into every assembly");
        AlphaLine.Rests.Should().Be(string.Equals(declared, "true", StringComparison.Ordinal));
    }
}
