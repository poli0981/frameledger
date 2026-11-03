// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Import;
using FrameLedger.Infrastructure.Art;
using FrameLedger.Infrastructure.Import;

namespace FrameLedger.Infrastructure.Tests.Import;

/// <summary>
/// beta.33 (owner decisions D78/D79, <c>spike-notes</c> §25): the Xbox app's games, read from a games folder laid out as the
/// owner's are — <c>&lt;Game&gt;\Content\</c> with <c>MicrosoftGame.config</c> and <c>appxmanifest.xml</c> in the shapes Valor
/// Mortis and FINAL FANTASY ship — and Windows' package answers stubbed, so the source's own decisions are what is tested.
/// </summary>
public sealed class XboxLibrarySourceTests : IDisposable
{
    private const string _valorConfig = """
        <?xml version="1.0" encoding="utf-8"?>
        <Game configVersion="1">
          <Identity Name="LyricalGamesLLC.ValorMortis" Publisher="CN=C32D4B9A-1AEC-416A-8124-789B35AD6753" Version="1.3.5124.0" />
          <ShellVisuals DefaultDisplayName="Valor Mortis" PublisherDisplayName="Lyrical Games LLC" StoreLogo="Resources\StoreLogo.png" Square150x150Logo="Resources\Logo.png" Square44x44Logo="Resources\SmallLogo.png" Square480x480Logo="Resources\Square480x480Logo.png" Description="ms-resource:AppDescription" ForegroundText="dark" BackgroundColor="#000040" SplashScreenImage="Resources\SplashScreen.png" />
          <ExecutableList>
            <Executable Name="ValorMortisMSG.exe" Id="AppValorMortisShipping" OverrideDisplayName="ms-resource:AppDisplayName" TargetDeviceFamily="PC" />
          </ExecutableList>
          <TitleId>76300F35</TitleId>
          <StoreId>9P861V1542BL</StoreId>
        </Game>
        """;

    private const string _valorManifest = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10">
          <Identity Name="LyricalGamesLLC.ValorMortis" Publisher="CN=C32D4B9A-1AEC-416A-8124-789B35AD6753" Version="1.3.5124.0" ProcessorArchitecture="x64" />
          <Properties>
            <DisplayName>Valor Mortis</DisplayName>
          </Properties>
          <Applications>
            <Application Id="AppValorMortisShipping" Executable="GameLaunchHelper.exe" EntryPoint="Windows.FullTrustApplication" />
          </Applications>
        </Package>
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "fl-xbox-" + Guid.NewGuid().ToString("N"));

    public XboxLibrarySourceTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Game(string folder, string config, string manifest, string configName = GdkPackageFiles.ConfigFileName)
    {
        string content = Path.Combine(_root, folder, XboxLibrarySource.ContentFolder);
        Directory.CreateDirectory(content);
        File.WriteAllText(Path.Combine(content, configName), config);
        File.WriteAllText(Path.Combine(content, GdkPackageFiles.ManifestFileName), manifest);
        return content;
    }

    [Fact]
    public async Task AnInstalledGdkPackageIsListedWithItsFamilyNameItsAumidAndItsContentFolder()
    {
        string content = Game("Valor Mortis", _valorConfig, _valorManifest);
        var packages = new Packages("LyricalGamesLLC.ValorMortis_72fag5ekhk96y");

        IReadOnlyList<StoreGame> games = await new XboxLibrarySource([_root], packages).ListAsync(Ct);

        games.Should().ContainSingle();
        StoreGame game = games[0];
        game.Platform.Should().Be("xbox");
        game.StoreId.Should().Be("LyricalGamesLLC.ValorMortis_72fag5ekhk96y", "the family name is stable across updates; the WindowsApps folder's name is not");
        game.Name.Should().Be("Valor Mortis");
        game.InstallDirectory.Should().Be(content, "the spelling the game's own process reports (§25), never the WindowsApps junction");
        game.ExePath.Should().BeNull("the package launches the stub; the locator picks the shipping executable that presents");
        game.Version.Should().Be("1.3.5124.0");
        game.LaunchKey.Should().Be("LyricalGamesLLC.ValorMortis_72fag5ekhk96y!AppValorMortisShipping", "the AUMID Windows' Start menu lists, which Play activates");
        packages.Asked.Should().Equal(("LyricalGamesLLC.ValorMortis", "CN=C32D4B9A-1AEC-416A-8124-789B35AD6753"));
    }

    [Fact]
    public async Task APackageWindowsDoesNotHaveInstalledForThisUserIsNotOffered()
    {
        Game("Left Behind", _valorConfig, _valorManifest);

        (await new XboxLibrarySource([_root], new Packages()).ListAsync(Ct)).Should().BeEmpty(
            "a folder the Xbox app left behind, or one installed for another user, is not a game this user can start");
    }

    [Fact]
    public async Task FoldersThatAreNotPackagesCostOnlyThemselves()
    {
        Game("Valor Mortis", _valorConfig, _valorManifest);
        Directory.CreateDirectory(Path.Combine(_root, "GameSave"));                       // the Xbox app's own folder, no Content
        Directory.CreateDirectory(Path.Combine(_root, "Half", "Content"));
        await File.WriteAllTextAsync(Path.Combine(_root, "Half", "Content", GdkPackageFiles.ConfigFileName), _valorConfig, Ct);   // no manifest
        Game("Broken", "<Game><Identity", _valorManifest);                                // will not parse
        Game("Nameless", "<Game configVersion=\"1\"><Identity Version=\"1.0.0.0\" /></Game>", _valorManifest);

        IReadOnlyList<StoreGame> games = await new XboxLibrarySource([_root, Path.Combine(_root, "no-such-folder")],
            new Packages("LyricalGamesLLC.ValorMortis_72fag5ekhk96y")).ListAsync(Ct);

        games.Select(static g => g.Name).Should().Equal("Valor Mortis");
    }

    [Fact]
    public async Task AResourceNameFallsBackToTheManifestsThenToTheFolders()
    {
        string config = _valorConfig.Replace("DefaultDisplayName=\"Valor Mortis\"", "DefaultDisplayName=\"ms-resource:AppDisplayName\"", StringComparison.Ordinal);
        Game("Valor Mortis", config, _valorManifest);
        Game("FINAL FANTASY", config.Replace("LyricalGamesLLC.ValorMortis", "39EA002F.FF1DF", StringComparison.Ordinal),
            _valorManifest.Replace("<DisplayName>Valor Mortis</DisplayName>", "<DisplayName>ms-resource:AppDisplayName</DisplayName>", StringComparison.Ordinal),
            configName: "MicrosoftGame.Config");

        IReadOnlyList<StoreGame> games = await new XboxLibrarySource([_root], new Packages("LyricalGamesLLC.ValorMortis_72fag5ekhk96y", "39EA002F.FF1DF_n746a19ndrrjg")).ListAsync(Ct);

        games.Select(static g => g.Name).Should().BeEquivalentTo(["Valor Mortis", "FINAL FANTASY"],
            "an ms-resource reference is not a name: the manifest's own DisplayName, else the game's folder (the FINAL FANTASY titles spell the config MicrosoftGame.Config)");
    }

    [Fact]
    public async Task TheApplicationThatRunsTheLaunchHelperIsTheOneActivated()
    {
        string manifest = _valorManifest.Replace(
            "<Application Id=\"AppValorMortisShipping\" Executable=\"GameLaunchHelper.exe\"",
            "<Application Id=\"Editor\" Executable=\"Editor.exe\" EntryPoint=\"Windows.FullTrustApplication\" /><Application Id=\"Game\" Executable=\"gamelaunchhelper.exe\"",
            StringComparison.Ordinal);
        Game("Valor Mortis", _valorConfig, manifest);

        (await new XboxLibrarySource([_root], new Packages("LyricalGamesLLC.ValorMortis_72fag5ekhk96y")).ListAsync(Ct))
            .Single().LaunchKey.Should().Be("LyricalGamesLLC.ValorMortis_72fag5ekhk96y!Game");
    }

    [Fact]
    public async Task AConfigWithADocumentTypeIsRefusedNotExpanded()
    {
        string withDtd = "<?xml version=\"1.0\"?><!DOCTYPE Game [<!ENTITY x \"Valor Mortis\">]>" + _valorConfig[(_valorConfig.IndexOf("<Game", StringComparison.Ordinal))..];
        Game("Valor Mortis", withDtd, _valorManifest);

        (await new XboxLibrarySource([_root], new Packages("LyricalGamesLLC.ValorMortis_72fag5ekhk96y")).ListAsync(Ct)).Should().BeEmpty(
            "the reader prohibits DTDs: a publisher's file is data, never something to expand");
    }

    [Fact]
    public void TheArtCacheReadsTheSquareLogoAndTheSplashFromThePackageAndNothingOutsideIt()
    {
        string content = Game("Valor Mortis", _valorConfig, _valorManifest);
        Directory.CreateDirectory(Path.Combine(content, "Resources"));
        File.WriteAllBytes(Path.Combine(content, "Resources", "Square480x480Logo.png"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(content, "Resources", "SplashScreen.scale-200.png"), [1, 2, 3, 4]);
        File.WriteAllBytes(Path.Combine(content, "Resources", "SplashScreen.scale-100.png"), [1, 2]);
        var record = new StoreGame("xbox", "LyricalGamesLLC.ValorMortis_72fag5ekhk96y", "Valor Mortis", content, ExePath: null, Version: null);

        Application.Art.GameArtFiles files = new XboxArtCache().Find(record);

        files.Square.Should().Be(Path.Combine(content, "Resources", "Square480x480Logo.png"));
        files.Hero.Should().Be(Path.Combine(content, "Resources", "SplashScreen.scale-200.png"), "the plain name is absent, so the largest scale variant beside it");
        files.Header.Should().Be(files.Hero);
        files.Cover.Should().BeNull("a GDK package ships no portrait cover");
        XboxArtCache.Image(content, @"..\..\outside.png").Should().BeNull("a name that escapes the package is not the package's picture");
        XboxArtCache.Image(content, @"C:\Windows\System32\shell32.dll").Should().BeNull();
    }

    /// <summary>Windows' package answers, stubbed: the family name is the one listed whose name part matches; installed = listed.</summary>
    private sealed class Packages(params string[] installed) : IInstalledPackages
    {
        public List<(string Name, string Publisher)> Asked { get; } = [];

        public string? FamilyName(string name, string publisher)
        {
            Asked.Add((name, publisher));
            return installed.FirstOrDefault(f => f.StartsWith(name + "_", StringComparison.Ordinal)) ?? name + "_0000000000000";
        }

        public bool IsInstalled(string familyName) => installed.Contains(familyName, StringComparer.Ordinal);
    }
}
