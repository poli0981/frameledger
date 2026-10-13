// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Import;
using FrameLedger.Application.Play;

namespace FrameLedger.Application.Tests.Play;

/// <summary>
/// beta.21 (Play, owner decision D59): each store's own way to start a game it installed, in the forms the stores write —
/// and nothing built from an id that does not look like that store's, because the link is handed to the shell.
/// </summary>
public sealed class StoreLaunchesTests
{
    private const string _galaxy = @"C:\Program Files\GOG Galaxy\GalaxyClient.exe";

    [Fact]
    public void SteamStartsItsGameByAppId()
    {
        StoreLaunch? launch = StoreLaunches.For(Record("steam", "3513350", @"D:\SteamLibrary\steamapps\common\Wuthering Waves"), null);

        launch.Should().Be(new StoreLaunch("steam", StoreLaunchKind.OpenLink, "steam://rungameid/3513350"), "the link of Steam's own game shortcuts");
    }

    [Fact]
    public void EpicNamesTheCatalogWhenTheManifestDoesAndTheAppNameOtherwise()
    {
        StoreGame withCatalog = Record("epic", "dc9d2e595d0e4650b35d659f90d41059", @"D:\Epic Games\AlanWake2") with
        {
            LaunchKey = "c4763f236d08423eb47b4c3008779c84:93f2a8c3547846eda966cb3c152a026e:dc9d2e595d0e4650b35d659f90d41059",
        };
        StoreLaunches.For(withCatalog, null)!.Target.Should().Be(
            "com.epicgames.launcher://apps/c4763f236d08423eb47b4c3008779c84%3A93f2a8c3547846eda966cb3c152a026e%3Adc9d2e595d0e4650b35d659f90d41059?action=launch&silent=true");

        StoreLaunches.For(Record("epic", "Fortnite", @"D:\Epic Games\Fortnite"), null)!.Target
            .Should().Be("com.epicgames.launcher://apps/Fortnite?action=launch&silent=true", "a manifest with no catalog names the AppName alone");

        StoreLaunches.For(withCatalog with { LaunchKey = "ns:item:another-app" }, null)!.Target
            .Should().Be("com.epicgames.launcher://apps/dc9d2e595d0e4650b35d659f90d41059?action=launch&silent=true", "a key naming another app is not this game's");
    }

    [Fact]
    public void GogRunsGalaxysOwnShortcutCommandAndNothingWithoutGalaxy()
    {
        StoreGame record = Record("gog", "1125815775", @"D:\another\gog\A Space for the Unbound - Prologue\");

        StoreLaunch? launch = StoreLaunches.For(record, _galaxy);

        launch.Should().NotBeNull();
        launch!.Kind.Should().Be(StoreLaunchKind.RunProgram);
        launch.Target.Should().Be(_galaxy);
        launch.Arguments.Should().Be("/command=runGame /gameId=1125815775 /path=\"D:\\another\\gog\\A Space for the Unbound - Prologue\"",
            "GALAXY's shortcut on the owner's PC, and no trailing backslash to escape the closing quote");
        launch.WorkingDirectory.Should().Be(@"C:\Program Files\GOG Galaxy");
        StoreLaunches.For(record, null).Should().BeNull("a GOG game without GALAXY has no hand-off; it is started directly");
    }

    [Fact]
    public void ItchAsksItsAppToLaunchTheGame()
    {
        StoreLaunches.For(Record("itch", "5015466", @"D:\another\it\hello-hello-world"), null)!.Target
            .Should().Be("itch://install?game_id=5015466&launch");
    }

    [Theory]
    [InlineData("steam", "730&evil=1")]
    [InlineData("steam", "")]
    [InlineData("steam", "12345678901234567890123")]
    [InlineData("gog", "1207658924 /command=x")]
    [InlineData("itch", "../games/1")]
    [InlineData("epic", "app?action=uninstall")]
    [InlineData("epic", "app name")]
    [InlineData("none", "730")]
    [InlineData("xbox", "730")]
    public void AnIdThatIsNotThatStoresShapeBuildsNothing(string platform, string id)
    {
        StoreLaunches.For(Record(platform, id, @"D:\Games\Some Game"), _galaxy).Should().BeNull();
    }

    [Fact]
    public void AGogFolderThatCannotStandInsideQuotesBuildsNothing()
    {
        StoreLaunches.For(Record("gog", "1", "relative\\folder"), _galaxy).Should().BeNull();
        StoreLaunches.For(Record("gog", "1", "D:\\a\"b"), _galaxy).Should().BeNull();
    }

    private static StoreGame Record(string platform, string id, string folder) => new(platform, id, "Name", folder, null, null);
}
