using System.Globalization;
using System.IO.Compression;
using System.Text;
using Dapper;
using FluentAssertions;
using FrameLedger.Application.Import;
using FrameLedger.Infrastructure.Import;
using Microsoft.Data.Sqlite;
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
        // A tool sold on Steam is not a game (2026-09-22): Borderless Gaming became a library row on the owner's machine and
        // the watcher recorded it. The import stops guessing; the user can still add its executable by hand.
        await File.WriteAllTextAsync(Path.Combine(rootApps, "appmanifest_388080.acf"),
            "\"AppState\"\n{\n\t\"appid\"\t\t\"388080\"\n\t\"name\"\t\t\"Borderless Gaming\"\n\t\"installdir\"\t\t\"Borderless Gaming\"\n}\n", Ct);

        IReadOnlyList<StoreGame> games = await new SteamLibrarySource(root).ListAsync(Ct);

        games.Should().HaveCount(2, "the malformed manifest costs one entry, not the library, and a known tool is not listed");
        games.Should().NotContain(static g => string.Equals(g.StoreId, "388080", StringComparison.Ordinal));
        SteamLibrarySource.KnownTools.Should().Contain("388080").And.Contain("228980", "Steamworks Common Redistributables");
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

        string itch = Sub("itch");
        string install = Sub("itch", "apps", "celeste");
        Directory.CreateDirectory(Path.Combine(install, ".itch"));
        using (FileStream f = File.Create(Path.Combine(install, ".itch", "receipt.json.gz")))
        using (var gz = new GZipStream(f, CompressionLevel.Fastest))
        {
            byte[] json = Encoding.UTF8.GetBytes("{\"game\":{\"id\":36776,\"title\":\"Celeste\"},\"files\":[\"Celeste.exe\"]}");
            await gz.WriteAsync(json, Ct);
        }

        Sub("itch", "apps", "empty");
        IReadOnlyList<StoreGame> receipts = await new ItchLibrarySource(itch).ListAsync(Ct);
        StoreGame c = receipts.Should().ContainSingle().Subject;
        c.Platform.Should().Be("itch");
        c.StoreId.Should().Be("36776");
        c.Name.Should().Be("Celeste");
        c.InstallDirectory.Should().Be(install);
        c.ExePath.Should().BeNull();

        (await new EpicLibrarySource(Path.Combine(_dir, "nowhere")).ListAsync(Ct)).Should().BeEmpty();
        (await new ItchLibrarySource(Path.Combine(_dir, "nowhere")).ListAsync(Ct)).Should().BeEmpty();
    }

    /// <summary>
    /// 2026-09-16: the itch app records its installs in butler.db — install locations the user chose (which need not be
    /// under %APPDATA%), one cave per install, and butler's own verdict naming the executable. The adapter read only
    /// the default apps\ folder and found nothing on a machine whose one location was elsewhere.
    /// </summary>
    [Fact]
    public async Task ItchReadsButlersDatabaseFirstAndReceiptsSecond()
    {
        string itch = Sub("itch");
        string location = Sub("elsewhere");
        string pob = Sub("elsewhere", "plethora-of-bullets-2");
        await File.WriteAllBytesAsync(Path.Combine(pob, "POB2.exe"), new byte[10], Ct);
        string dontGo = Sub("elsewhere", "dont-go");
        Directory.CreateDirectory(Path.Combine(dontGo, "DONT GO"));
        await File.WriteAllBytesAsync(Path.Combine(dontGo, "DONT GO", "PIZZA MAN.exe"), new byte[10], Ct);
        string custom = Sub("custom", "web-game");
        await WriteReceiptAsync(Path.Combine(custom, ".itch", "receipt.json.gz"), 777, "Web Game", Ct);
        string receiptOnly = Sub("itch", "apps", "celeste");
        await WriteReceiptAsync(Path.Combine(receiptOnly, ".itch", "receipt.json.gz"), 36776, "Celeste", Ct);
        Directory.CreateDirectory(Path.Combine(dontGo, ".itch"));
        await WriteReceiptAsync(Path.Combine(dontGo, ".itch", "receipt.json.gz"), 3212881, "DONT GO (receipt)", Ct);
        await WriteButlerAsync(Path.Combine(itch, "db", "butler.db"), location, [
            (1659887, "plethora-of-bullets-2", "", "Plethora of Bullets 2", "{\"basePath\":\"x\",\"candidates\":[{\"path\":\"POB2.exe\",\"flavor\":\"windows\",\"arch\":\"amd64\"}]}"),
            (3212881, "dont-go", "", "DONT GO", "{\"candidates\":[{\"path\":\"DONT GO/PIZZA MAN.exe\",\"flavor\":\"windows\"}]}"),
            (777, "", custom, "Web Game", "{\"candidates\":[{\"path\":\"index.html\",\"flavor\":\"html\"}]}"),
            (999, "gone", "", null, "not json"),
        ]);
        List<string> log = [];

        IReadOnlyList<StoreGame> games = await new ItchLibrarySource(itch, log.Add).ListAsync(Ct);

        games.Should().HaveCount(5, "four caves and one receipt-only install; the receipt beside a cave is the same row");
        StoreGame p = games.Single(g => string.Equals(g.StoreId, "1659887", StringComparison.Ordinal));
        p.Name.Should().Be("Plethora of Bullets 2");
        p.InstallDirectory.Should().Be(pob);
        p.ExePath.Should().Be(Path.Combine(pob, "POB2.exe"), "butler's verdict names the executable");
        StoreGame d = games.Single(g => string.Equals(g.StoreId, "3212881", StringComparison.Ordinal));
        d.Name.Should().Be("DONT GO", "the database's title wins over the receipt's");
        d.ExePath.Should().Be(Path.Combine(dontGo, "DONT GO", "PIZZA MAN.exe"), "a verdict path is relative to the install and uses forward slashes");
        games.Single(g => string.Equals(g.StoreId, "777", StringComparison.Ordinal)).Should().BeEquivalentTo(new { InstallDirectory = custom, ExePath = (string?)null }, "a custom install folder is the install; an html flavor is not an executable");
        games.Single(g => string.Equals(g.StoreId, "999", StringComparison.Ordinal)).Should().BeEquivalentTo(new { Name = "gone", ExePath = (string?)null }, "no title anywhere falls back to the folder name; a verdict that will not parse leaves the guess to the locator");
        games.Single(g => string.Equals(g.StoreId, "36776", StringComparison.Ordinal)).InstallDirectory.Should().Be(receiptOnly, "apps\\ is still scanned for what the database does not list");
        log.Should().ContainSingle().Which.Should().Contain("butler.db read: 1 location(s), 4 cave(s), 4 title(s)").And.Contain("1 receipt(s), 1 new", "only the apps folder is scanned for receipts; the one beside a cave elsewhere is read for its title, not counted here");
    }

    [Fact]
    public async Task ItchWithoutADatabaseOrWithAnUnreadableOneStillReadsReceipts()
    {
        string itch = Sub("itch");
        await WriteReceiptAsync(Path.Combine(Sub("itch", "apps", "celeste"), ".itch", "receipt.json.gz"), 36776, "Celeste", Ct);
        List<string> log = [];

        (await new ItchLibrarySource(itch, log.Add).ListAsync(Ct)).Should().ContainSingle().Which.StoreId.Should().Be("36776");
        log.Should().ContainSingle().Which.Should().Contain("no ").And.Contain("butler.db");

        Directory.CreateDirectory(Path.Combine(itch, "db"));
        await File.WriteAllTextAsync(Path.Combine(itch, "db", "butler.db"), "this is not a database", Ct);
        log.Clear();
        (await new ItchLibrarySource(itch, log.Add).ListAsync(Ct)).Should().ContainSingle();
        log.Should().ContainSingle().Which.Should().Contain("butler.db unreadable");

        log.Clear();
        (await new ItchLibrarySource(Path.Combine(_dir, "nowhere"), log.Add).ListAsync(Ct)).Should().BeEmpty();
        log.Should().ContainSingle().Which.Should().Contain("not installed");
    }

    private static async Task WriteReceiptAsync(string path, long id, string title, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        FileStream f = File.Create(path);
        await using (f.ConfigureAwait(false))
        {
            var gz = new GZipStream(f, CompressionLevel.Fastest);
            await using (gz.ConfigureAwait(false))
            {
                byte[] json = Encoding.UTF8.GetBytes("{\"game\":{\"id\":" + id.ToString(CultureInfo.InvariantCulture) + ",\"title\":\"" + title + "\"}}");
                await gz.WriteAsync(json, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>The three butler tables the adapter reads, with the columns it names (the real ones carry more).</summary>
    private static async Task WriteButlerAsync(string path, string locationPath,
        IReadOnlyList<(long GameId, string Folder, string Custom, string? Title, string Verdict)> caves)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        await using (c.ConfigureAwait(false))
        {
            await c.OpenAsync(Ct).ConfigureAwait(false);
            await c.ExecuteAsync(new CommandDefinition(
                """
                CREATE TABLE install_locations (id TEXT PRIMARY KEY, path TEXT);
                CREATE TABLE games (id INTEGER PRIMARY KEY, title TEXT, classification TEXT);
                CREATE TABLE caves (id TEXT PRIMARY KEY, game_id INTEGER, install_location_id TEXT, install_folder_name TEXT, custom_install_folder TEXT, verdict TEXT);
                INSERT INTO install_locations VALUES ('loc-1', @location);
                """, new { location = locationPath }, cancellationToken: Ct)).ConfigureAwait(false);
            foreach ((long gameId, string folder, string custom, string? title, string verdict) in caves)
            {
                if (title is not null)
                {
                    await c.ExecuteAsync(new CommandDefinition("INSERT INTO games VALUES (@id, @title, 'game')", new { id = gameId, title }, cancellationToken: Ct)).ConfigureAwait(false);
                }

                await c.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO caves VALUES (@id, @gameId, 'loc-1', @folder, @custom, @verdict)",
                    new { id = Guid.NewGuid().ToString("N"), gameId, folder, custom, verdict }, cancellationToken: Ct)).ConfigureAwait(false);
            }
        }
    }

    [Fact]
    public async Task TheLocatorDropsHelpersAndStopsAtFourLevels()
    {
        string game = Sub("Game");
        string bin = Sub("Game", "Binaries", "Win64");
        await File.WriteAllBytesAsync(Path.Combine(game, "Launcher.exe"), new byte[10], Ct);
        await File.WriteAllBytesAsync(Path.Combine(game, "UnityCrashHandler64.exe"), new byte[5000], Ct);
        await File.WriteAllBytesAsync(Path.Combine(bin, "Title-Win64-Shipping.exe"), new byte[4000], Ct);
        await File.WriteAllBytesAsync(Path.Combine(bin, "EasyAntiCheat_Setup.exe"), new byte[9000], Ct);
        string deep = Sub("Game", "a", "b", "c", "d", "e");
        await File.WriteAllBytesAsync(Path.Combine(deep, "TooDeep.exe"), new byte[99999], Ct);

        var locator = new ExecutableLocator();
        locator.PickPrimary(game).Should().Be(Path.Combine(bin, "Title-Win64-Shipping.exe"), "helpers are dropped by name, the fifth level is not walked, and the Unreal shipping binary outranks the root launcher (ExecutableLocatorTests holds the ranking)");
        locator.PickPrimary(Path.Combine(_dir, "nowhere")).Should().BeNull();
        ExecutableLocator.IsHelper("vc_redist.x64").Should().BeTrue();
        ExecutableLocator.IsHelper("Cyberpunk2077").Should().BeFalse();
    }
}
