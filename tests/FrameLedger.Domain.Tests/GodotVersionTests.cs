// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Domain.Tests;

/// <summary>
/// The Godot version (beta.19): the owner's two Godot installs as measured on 2026-10-12, and the executable's version
/// resource taken only while it is still the engine's.
/// </summary>
public sealed class GodotVersionTests
{
    [Fact]
    public void TheTwoMeasuredInstallsReadFromTheirPacks()
    {
        GodotVersion.Decide(new GodotBuildFacts { PackVersion = [4, 2, 2], ExeProductName = "Godot Engine", ExeProductVersion = "4.2.2.stable.official" })
            .Should().Be(new EngineVersionReading("4.2.2", EngineVersionSource.GodotPack), "Plethora of Bullets 2");
        GodotVersion.Decide(new GodotBuildFacts { PackVersion = [4, 4, 1], ExeProductName = "xiandie", ExeProductVersion = "0.1.0.3" })
            .Should().Be(new EngineVersionReading("4.4.1", EngineVersionSource.GodotPack), "衔蝶");
    }

    [Fact]
    public void AnAppendedPackIsSaidAsSuchAndAZeroPatchIsNotWritten() =>
        GodotVersion.Decide(new GodotBuildFacts { PackVersion = [4, 3, 0], PackEmbedded = true })
            .Should().Be(new EngineVersionReading("4.3", EngineVersionSource.GodotPackEmbedded), "Godot names 4.3.0 as 4.3");

    [Theory]
    [InlineData("Godot Engine", "4.2.2.stable.official", "4.2.2")]
    [InlineData("Godot Engine", "4.2.2.stable.mono.official", "4.2.2")]
    [InlineData("Godot Engine", "4.3.stable", "4.3")]
    [InlineData("Godot Engine", "3.5.3", "3.5.3")]
    [InlineData("xiandie", "0.1.0.3", null)]
    [InlineData("Godot Engine", "not a version", null)]
    [InlineData(null, "4.2.2.stable.official", null)]
    public void WithoutAPackTheExecutableSpeaksOnlyWhileItIsStillTheEngines(string? productName, string? productVersion, string? expected)
    {
        EngineVersionReading r = GodotVersion.Decide(new GodotBuildFacts { ExeProductName = productName, ExeProductVersion = productVersion });

        r.Version.Should().Be(expected);
        r.Source.Should().Be(expected is null ? null : EngineVersionSource.GodotExe);
    }

    [Fact]
    public void APackHeaderNoGodotReleaseCarriesIsNoVersion() =>
        GodotVersion.Decide(new GodotBuildFacts { PackVersion = [0, 0, 0] }).Should().Be(EngineVersionReading.None);
}
