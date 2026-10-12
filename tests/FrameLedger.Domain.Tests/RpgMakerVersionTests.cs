// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Domain.Tests;

/// <summary>
/// RPG Maker's maker and version (beta.19): the owner's six RPG Maker installs as measured on 2026-10-12 — MZ and MV by their
/// core script's header, VX Ace by its RGSS3 library — and the RGSS generations by name.
/// </summary>
public sealed class RpgMakerVersionTests
{
    [Theory]
    [InlineData("A Doll's Locket - Remake", "rmmz_core.js", "1.10.0", "1.10.0", EngineVersionSource.RpgMakerMzCore)]
    [InlineData("Flower in Us", "rpg_core.js", "1.6.2", "1.6.2", EngineVersionSource.RpgMakerMvCore)]
    [InlineData("996IR", "rpg_core.js", "1.6.2", "1.6.2", EngineVersionSource.RpgMakerMvCore)]
    [InlineData("HELLO, HELLO WORLD!", "rpg_core.js", "1.6.1", "1.6.1", EngineVersionSource.RpgMakerMvCore)]
    public void MzAndMvReadFromTheirCoreScript(string title, string script, string header, string version, string source) =>
        RpgMakerVersion.Decide(new RpgMakerBuildFacts { CoreScript = script, CoreScriptVersion = header })
            .Should().Be(new EngineVersionReading(version, source), title);

    [Fact]
    public void VxAceReadsFromItsRgss3Library() =>
        RpgMakerVersion.Decide(new RpgMakerBuildFacts { RgssLibrary = "RGSS301.dll", RgssFileVersion = [3, 0, 1, 1] })
            .Should().Be(new EngineVersionReading("3.0.1", EngineVersionSource.Rgss3Library), "City of Bones and Träumerei Chapter 2");

    [Theory]
    [InlineData("RGSS104E.dll", "1.0.4", EngineVersionSource.Rgss1Library)]
    [InlineData("RGSS102J.dll", "1.0.2", EngineVersionSource.Rgss1Library)]
    [InlineData("RGSS202E.dll", "2.0.2", EngineVersionSource.Rgss2Library)]
    [InlineData("rgss300.dll", "3.0.0", EngineVersionSource.Rgss3Library)]
    public void WithoutAVersionResourceTheLibrarysNameCarriesIt(string library, string version, string source) =>
        RpgMakerVersion.Decide(new RpgMakerBuildFacts { RgssLibrary = library })
            .Should().Be(new EngineVersionReading(version, source));

    [Fact]
    public void ALibraryWhoseNumbersAreNotItsGenerationsKeepsTheNameAndNothingElseIsAVersion()
    {
        RpgMakerVersion.Decide(new RpgMakerBuildFacts { RgssLibrary = "RGSS301.dll", RgssFileVersion = [1, 0, 0, 1] })
            .Should().Be(new EngineVersionReading("3.0.1", EngineVersionSource.Rgss3Library));
        RpgMakerVersion.Decide(new RpgMakerBuildFacts { RgssLibrary = "Game.dll" }).Should().Be(EngineVersionReading.None);
        RpgMakerVersion.Decide(new RpgMakerBuildFacts { CoreScript = "rmmz_core.js", CoreScriptVersion = "v1" }).Should().Be(EngineVersionReading.None);
        RpgMakerVersion.Decide(new RpgMakerBuildFacts()).Should().Be(EngineVersionReading.None);
    }
}
