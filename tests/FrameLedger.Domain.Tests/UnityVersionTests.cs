// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Detection;

namespace FrameLedger.Domain.Tests;

/// <summary>
/// The Unity version ladder (beta.19). The cases are the owner's fifteen Unity installs, measured read-only on 2026-10-12
/// (<c>05_DETECTION</c> §Engine version — Unity, Godot, RPG Maker) — their numbers only, never their files.
/// </summary>
public sealed class UnityVersionTests
{
    public static TheoryData<string, string?, int[]?, string?, string?, string?> Measured => new()
    {
        // title, UnityPlayer.dll ProductVersion, its numeric file version, data header version, → version, source
        { "A Space for the Unbound - Prologue", "2019.4.1.15122501", [2019, 4, 1, 15122501], "2019.4.1f1", "2019.4.1f1", EngineVersionSource.UnityData },
        { "DONT GO", "2022.3.39f1 (4e1b0f82c39a)", [2022, 3, 39, 5118735], "2022.3.39f1", "2022.3.39f1", EngineVersionSource.UnityPlayer },
        { "Dialoop", "6000.3.3f1 (ef04196de0d6)", [6000, 3, 3, 15664153], "6000.3.3f1", "6000.3.3f1", EngineVersionSource.UnityPlayer },
        { "Broken Shell Demo", "6000.0.58f2 (92dee566b325)", [6000, 0, 58, 9625317], "6000.0.58f2", "6000.0.58f2", EngineVersionSource.UnityPlayer },
        { "Deadly Heart Gambit", "2022.3.32f1 (c8300dc0a3fa)", [2022, 3, 32, 13119501], "2022.3.32f1", "2022.3.32f1", EngineVersionSource.UnityPlayer },
        { "DreadFields", "6000.3.17f1 (cf0352b38e81)", [6000, 3, 17, 13566802], "6000.3.17f1", "6000.3.17f1", EngineVersionSource.UnityPlayer },
        { "GIRLS' FRONTLINE 2: EXILIUM", "2019.4.40.16762411", [2019, 4, 40, 16762411], null, "2019.4.40", EngineVersionSource.UnityPlayerFile },
        { "Kemono Teatime", "2022.3.51f1 (9f9d16c45e54)", [2022, 3, 51, 10460438], "2022.3.51f1", "2022.3.51f1", EngineVersionSource.UnityPlayer },
        { "March Out! Playtest", "2022.3.62f3 (96770f904ca7)", [2022, 3, 62, 9860879], "2022.3.62f3", "2022.3.62f3", EngineVersionSource.UnityPlayer },
        { "Umamusume: Pretty Derby", "2022.3.62f2 (7670c08855a9)", [2022, 3, 62, 7762112], "2022.3.62f2", "2022.3.62f2", EngineVersionSource.UnityPlayer },
        { "Walto", "6000.3.2f1 (a9779f353c9b)", [6000, 3, 2, 11106207], "6000.3.2f1", "6000.3.2f1", EngineVersionSource.UnityPlayer },
        { "Yasha: Legends of the Demon Blade", "2022.3.54f1 (129125d4e700)", [2022, 3, 54, 1216805], "2022.3.54f1", "2022.3.54f1", EngineVersionSource.UnityPlayer },
        { "Yu-Gi-Oh! Master Duel", "6000.0.61f1 (74a0adb02c31)", [6000, 0, 61, 7643309], null, "6000.0.61f1", EngineVersionSource.UnityPlayer },
        { "帰路", "6000.0.58f2 (92dee566b325)", [6000, 0, 58, 9625317], "6000.0.58f2", "6000.0.58f2", EngineVersionSource.UnityPlayer },
        { "Legends of Runeterra", "2022.3.62f2 (ac7faeb0984d)", [2022, 3, 62, 11304878], "2022.3.62f2", "2022.3.62f2", EngineVersionSource.UnityPlayer },
    };

    [Theory]
    [MemberData(nameof(Measured))]
    public void TheFifteenMeasuredInstallsReadAsMeasured(string title, string? product, int[]? fileVersion, string? header, string? version, string? source)
    {
        EngineVersionReading r = UnityVersion.Decide(new UnityBuildFacts { PlayerProductVersion = product, PlayerFileVersion = fileVersion, DataHeaderVersion = header });

        r.Version.Should().Be(version, title);
        r.Source.Should().Be(source, title);
    }

    [Theory]
    [InlineData("2022.3.39f1 (4e1b0f82c39a)", "2022.3.39f1")]
    [InlineData("6000.0.58f2", "6000.0.58f2")]
    [InlineData("5.6.4p2", "5.6.4p2")]
    [InlineData("2019.4.1.15122501", null)]
    [InlineData("10.0.29648.1000", null)]
    [InlineData("1.2.3f1", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void OnlyAnEditorVersionOfAMajorUnityShippedIsOne(string? text, string? expected) =>
        UnityVersion.EditorVersion(text).Should().Be(expected);

    /// <summary>A library's numbers that no Unity release carries — a system DLL's — are nothing, not a Unity version.</summary>
    [Fact]
    public void NumbersNoUnityReleaseCarriesAreNoVersion() =>
        UnityVersion.Decide(new UnityBuildFacts { PlayerProductVersion = "10.0.29648.1000", PlayerFileVersion = [10, 0, 29648, 1000] })
            .Should().Be(EngineVersionReading.None);
}
