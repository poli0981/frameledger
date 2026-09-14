using System.IO.Compression;
using System.Text;
using FluentAssertions;
using FrameLedger.Application.Import;
using FrameLedger.Infrastructure.Import;
using Microsoft.Win32;

namespace FrameLedger.Infrastructure.Tests.Import;

/// <summary>
/// The four stores' readers over fixtures written by the test (P4 PR-4): Steam's two-file shape across two library
/// folders, GOG's registry keys under a test key, Epic's <c>.item</c> manifests, itch's gzipped receipts — and the
/// executable locator's guess. A store that is not there is an empty list; a file that will not parse costs one entry.
/// </summary>
public sealed class StoreLibrarySourcesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-stores-" + Guid.NewGuid().ToString("N"));

    public StoreLibrarySourcesTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Sub(params string[] parts)
    {
        string p = Path.Combine([_dir, .. parts]);
        Directory.CreateDirectory(p);
        return p;
    }

    [Fact]
    public async Task SteamListsEveryManifestInEveryLibraryFolderAndNamesNoExecutable()
    {
        string root = Sub("Steam");
        string second = Sub("Library2");
        string rootApps = Sub("Steam", "steamapps");
        string secondApps = Sub("Library2", "steamapps");
        await File.WriteAllTextAsync(Path.Combine(rootApps, "libraryfolders.vdf"),
            "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"" + root.Replace("\\", "\\\\", StringComparison.Ordinal) + "\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"" + second.Replace("\\", "\\\\", StringComparison.Ordinal) + "\"\n\t}\n}\n", Ct);
        await File.WriteAllTextAsync(Path.Combine(rootApps, "appmanifest_1091500.acf"),
            "\"AppState\"\n{\n\t\"appid\"\t\t\"1091500\"\n\t\"name\"\t\t\"Cyberpunk 2077\"\n\t\"installdir\"\t\t\"Cyberpunk 2077\"\n\t\"buildid\"\t\t\"15877371\"\n}\n", Ct);
        await File.WriteAllTextAsync(Path.Combine(secondApps, "appmanifest_2358720.acf"),
            "\"AppState\"\n{\n\t\"appid\"\t\t\"2358720\"\n\t\"name\"\t\t\"Black Myth: Wukong\"\n\t\"installdir\"\t\t\"BlackMythWukong\"\n}\n", Ct);
        await File.WriteAllTextAsync(Path.Combine(secondApps, "appmanifest_bad.acf"), "not key values at all {", Ct);

        IReadOnlyList<StoreGame> games = await new SteamLibrarySource(root).ListAsync(Ct);

        games.Should().HaveCount(2, "the malformed manifest costs one entry, not the library");
        StoreGame cp = games.Single(static g => string.Equals(g.StoreId, "1091500", StringComparison.Ordinal));
        cp.Platform.Should().Be("steam");
        cp.Name.Should().Be("Cyberpunk 2077");
        cp.InstallDirectory.Should().Be(Path.Combine(rootApps, "common", "Cyberpunk 2077"));
        cp.Version.Should().Be("15877371");
        cp.ExePath.Should().BeNull("Steam does not say which executable is the game");
        games.Single(static g => string.Equals(g.StoreId, "2358720", StringComparison.Ordinal)).InstallDirectory.Should().StartWith(secondApps);

        (await new SteamLibrarySource(Path.Combine(_dir, "nowhere")).ListAsync(Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task GogReadsItsRegistryKeysUnderWhateverKeyItIsGiven()
    {
        string own = @"SOFTWARE\FrameLedger.Test\" + Guid.NewGuid().ToString("N");
        string key = own + @"\GOG.com\Games";
        try
        {
            using (RegistryKey games = Registry.CurrentUser.CreateSubKey(key + @"\1207658924", writable: true))
            {
                games.SetValue("gameName", "The Witcher 3");
                games.SetValue("path", @"D:\GOG Galaxy\Games\The Witcher 3");
                games.SetValue("exe", @"D:\GOG Galaxy\Games\The Witcher 3\bin\x64\witcher3.exe");
                games.SetValue("ver", "4.04");
            }

            using (RegistryKey broken = Registry.CurrentUser.CreateSubKey(key + @"\9999", writable: true))
            {
                broken.SetValue("gameName", "no path");
            }

            IReadOnlyList<StoreGame> list = await new GogLibrarySource(RegistryHive.CurrentUser, key).ListAsync(Ct);
            StoreGame w3 = list.Should().ContainSingle().Subject;
            w3.Platform.Should().Be("gog");
            w3.StoreId.Should().Be("1207658924");
            w3.Name.Should().Be("The Witcher 3");
            w3.ExePath.Should().Be(@"D:\GOG Galaxy\Games\The Witcher 3\bin\x64\witcher3.exe", "GOG names the launch executable");
            w3.Version.Should().Be("4.04");

            (await new GogLibrarySource(RegistryHive.CurrentUser, key + @"\absent").ListAsync(Ct)).Should().BeEmpty();
        }
        finally
        {
            // Only this test's subtree: FrameLedger.Test is shared with VkLayerRegistrationTests, which may be running.
            Registry.CurrentUser.DeleteSubKeyTree(own, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public async Task EpicReadsItemManifestsAndItchReadsGzippedReceipts()
    {
        string manifests = Sub("Manifests");
        await File.WriteAllTextAsync(Path.Combine(manifests, "A1.item"),
            "{\"DisplayName\":\"Alan Wake 2\",\"InstallLocation\":\"D:\\\\Epic Games\\\\AlanWake2\",\"LaunchExecutable\":\"AlanWake2.exe\",\"AppName\":\"dcbb1ebd\",\"AppVersionString\":\"1.2.3\"}", Ct);
        await File.WriteAllTextAsync(Path.Combine(manifests, "B2.item"), "{\"DisplayName\":\"No location\"}", Ct);
        await File.WriteAllTextAsync(Path.Combine(manifests, "C3.item"), "{ not json", Ct);
        IReadOnlyList<StoreGame> epic = await new EpicLibrarySource(manifests).ListAsync(Ct);
        StoreGame aw = epic.Should().ContainSingle().Subject;
        aw.Platform.Should().Be("epic");
        aw.StoreId.Should().Be("dcbb1ebd");
        aw.ExePath.Should().Be(@"D:\Epic Games\AlanWake2\AlanWake2.exe");
        aw.Version.Should().Be("1.2.3");

        string apps = Sub("itch", "apps");
        string install = Sub("itch", "apps", "celeste");
        Directory.CreateDirectory(Path.Combine(install, ".itch"));
        using (FileStream f = File.Create(Path.Combine(install, ".itch", "receipt.json.gz")))
        using (var gz = new GZipStream(f, CompressionLevel.Fastest))
        {
            byte[] json = Encoding.UTF8.GetBytes("{\"game\":{\"id\":36776,\"title\":\"Celeste\"},\"files\":[\"Celeste.exe\"]}");
            await gz.WriteAsync(json, Ct);
        }

        Sub("itch", "apps", "empty");
        IReadOnlyList<StoreGame> itch = await new ItchLibrarySource(apps).ListAsync(Ct);
        StoreGame c = itch.Should().ContainSingle().Subject;
        c.Platform.Should().Be("itch");
        c.StoreId.Should().Be("36776");
        c.Name.Should().Be("Celeste");
        c.InstallDirectory.Should().Be(install);
        c.ExePath.Should().BeNull();

        (await new EpicLibrarySource(Path.Combine(_dir, "nowhere")).ListAsync(Ct)).Should().BeEmpty();
        (await new ItchLibrarySource(Path.Combine(_dir, "nowhere")).ListAsync(Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task TheLocatorTakesTheLargestNonHelperExecutableWithinThreeLevels()
    {
        string game = Sub("Game");
        string bin = Sub("Game", "Binaries", "Win64");
        await File.WriteAllBytesAsync(Path.Combine(game, "Launcher.exe"), new byte[10], Ct);
        await File.WriteAllBytesAsync(Path.Combine(game, "UnityCrashHandler64.exe"), new byte[5000], Ct);
        await File.WriteAllBytesAsync(Path.Combine(bin, "Title-Win64-Shipping.exe"), new byte[4000], Ct);
        await File.WriteAllBytesAsync(Path.Combine(bin, "EasyAntiCheat_Setup.exe"), new byte[9000], Ct);
        string deep = Sub("Game", "a", "b", "c", "d");
        await File.WriteAllBytesAsync(Path.Combine(deep, "TooDeep.exe"), new byte[99999], Ct);

        var locator = new ExecutableLocator();
        locator.PickPrimary(game).Should().Be(Path.Combine(bin, "Title-Win64-Shipping.exe"), "helpers are dropped by name, the fourth level is not walked, the largest survivor wins");
        locator.PickPrimary(Path.Combine(_dir, "nowhere")).Should().BeNull();
        ExecutableLocator.IsHelper("vc_redist.x64").Should().BeTrue();
        ExecutableLocator.IsHelper("Cyberpunk2077").Should().BeFalse();
    }
}
