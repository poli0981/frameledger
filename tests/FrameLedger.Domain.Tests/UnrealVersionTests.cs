// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Domain.Tests;

/// <summary>
/// The Unreal Engine version ladder (beta.10). The first ten cases are the ten shipping executables measured read-only on
/// the owner's machine on 2026-09-27 (<c>05_DETECTION</c> §Engine version) — their numbers only, never their files.
/// </summary>
public sealed class UnrealVersionTests
{
    public static TheoryData<string, int[], string?, string?, string?, string[], string?, string?> Measured => new()
    {
        // title, fixed file version, FileVersion text, ProductVersion text, CompanyName, branch lines in the bytes, → version, source
        { "Black Myth: Wukong", [5, 0, 0, 0], null, null, null, ["5.0"], "5.0.0", EngineVersionSource.UnrealBranchAndFile },
        { "Cronos: The New Dawn demo", [1, 2, 0, 0], "1.2.0.0 (21.10.2025)", "1.0.0.0", "Bloober Team SA", [], null, null },
        { "Clair Obscur: Expedition 33", [5, 4, 4, 0], null, null, null, [], "5.4.4", EngineVersionSource.UnrealFileVersion },
        { "Hell Is Us", [5, 5, 4, 0], "UE5-CL-0", "UE5-CL-0", "Nacon", [], "5.5.4", EngineVersionSource.UnrealFileVersion },
        { "Lies of P", [4, 27, 2, 0], null, null, null, [], "4.27.2", EngineVersionSource.UnrealFileVersion },
        { "Martha Is Dead", [4, 27, 0, 0], null, null, null, ["4.27"], "4.27.0", EngineVersionSource.UnrealBranchAndFile },
        { "Rune Factory: Guardians of Azuma", [1, 0, 3, 0], null, null, null, [], null, null },
        { "SILENT HILL 2", [1, 0, 0, 5], "1.0.0.5 (21/01/2025)", "1.0.0.5", "Konami", ["5.1"], "5.1", EngineVersionSource.UnrealBranch },
        { "UMIGARI", [5, 5, 4, 0], "++UE5+Release-5.5-CL-40574608", "++UE5+Release-5.5-CL-40574608", "Epic Games, Inc.", [], "5.5.4", EngineVersionSource.UnrealBranchAndFile },
        { "AbyssMemory", [4, 26, 1, 0], null, null, null, ["4.26"], "4.26.1", EngineVersionSource.UnrealBranchAndFile },
    };

    [Theory]
    [MemberData(nameof(Measured))]
    public void TheTenMeasuredTitlesReadAsMeasured(string title, int[] fixedVersion, string? fileText, string? productText, string? company,
        string[] branches, string? version, string? source)
    {
        EngineVersionReading r = UnrealVersion.Decide(new UnrealBuildFacts
        {
            FixedFileVersion = fixedVersion,
            FileVersionText = fileText,
            ProductVersionText = productText,
            CompanyName = company,
            BranchVersions = branches,
        });

        r.Version.Should().Be(version, title);
        r.Source.Should().Be(source, title);
    }

    /// <summary>A studio's own 4.x number with version strings of its own is not the engine's: nothing says Unreal wrote it.</summary>
    [Fact]
    public void AStudiosOwnFourPointSomethingIsNotTakenForTheEngine()
    {
        UnrealVersion.Decide(new UnrealBuildFacts
        {
            FixedFileVersion = [4, 2, 0, 0],
            FileVersionText = "4.2.0.0",
            ProductVersionText = "4.2",
            CompanyName = "Some Studio",
        }).Should().Be(EngineVersionReading.None);
    }

    /// <summary>Two release lines and no agreeing file version: either answer would be a guess.</summary>
    [Fact]
    public void TwoDisagreeingBranchNamesWithoutAFileVersionAreNoVersion()
    {
        UnrealVersion.Decide(new UnrealBuildFacts { FixedFileVersion = [1, 0, 0, 0], BranchVersions = ["4.27", "5.1"] })
            .Should().Be(EngineVersionReading.None);
    }

    /// <summary>...but a file version agreeing with one of them picks it, patch and all.</summary>
    [Fact]
    public void AFileVersionAgreeingWithOneOfTwoBranchNamesPicksIt()
    {
        UnrealVersion.Decide(new UnrealBuildFacts { FixedFileVersion = [5, 1, 1, 0], BranchVersions = ["4.27", "5.1"] })
            .Should().Be(new EngineVersionReading("5.1.1", EngineVersionSource.UnrealBranchAndFile));
    }

    [Theory]
    [InlineData(4, 28, 0)]     // past the last UE4 line
    [InlineData(5, 10, 0)]     // past the headroom this build allows
    [InlineData(3, 0, 0)]      // UE3 has no numbered release lines
    [InlineData(6, 0, 0)]
    [InlineData(5, 3, 21)]     // a patch no engine release has come near
    public void AFileVersionThatCannotBeAnEngineReleaseIsNotOne(int major, int minor, int patch)
    {
        UnrealVersion.Decide(new UnrealBuildFacts { FixedFileVersion = [major, minor, patch, 0] })
            .Should().Be(EngineVersionReading.None);
    }

    /// <summary>A version string naming the OTHER engine generation does not vouch for the number.</summary>
    [Fact]
    public void AStringNamingTheOtherGenerationDoesNotVouchForTheNumber()
    {
        UnrealVersion.Decide(new UnrealBuildFacts { FixedFileVersion = [4, 27, 2, 0], FileVersionText = "UE5-CL-0" })
            .Should().Be(EngineVersionReading.None);
    }

    [Theory]
    [InlineData("5.1", "5.1")]
    [InlineData("4.27", "4.27")]
    [InlineData(" 5.4 ", "5.4")]
    [InlineData("5.01", "5.1")]
    [InlineData("5", null)]
    [InlineData("5.1.2", null)]
    [InlineData("6.0", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void AReleaseLineIsMajorDotMinorOfAnEngineGeneration(string? text, string? line) =>
        UnrealVersion.ReleaseLine(text).Should().Be(line);

    [Fact]
    public void NoFactsAtAllIsNoVersion() =>
        UnrealVersion.Decide(new UnrealBuildFacts()).Should().Be(EngineVersionReading.None);

    [Fact]
    public void EverySourceIdIsStableAndDistinct()
    {
        string[] ids =
        [
            EngineVersionSource.UnrealBranchAndFile, EngineVersionSource.UnrealBranch, EngineVersionSource.UnrealFileVersion,
            .. Enum.GetValues<VersionExtractorType>().Select(EngineVersionSource.Of),
        ];

        ids.Should().OnlyHaveUniqueItems();
        EngineVersionSource.UnrealBranchAndFile.Should().Be("unreal_branch_file", "the ids are stored in games.engine_version_source");
        EngineVersionSource.UnrealBranch.Should().Be("unreal_branch");
        EngineVersionSource.UnrealFileVersion.Should().Be("unreal_file_version");
    }
}
