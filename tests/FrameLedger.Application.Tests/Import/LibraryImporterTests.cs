using FluentAssertions;
using FrameLedger.Application.Import;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Tests.Recording;
using FrameLedger.Application.Watch;
using FrameLedger.Domain.Consent;

namespace FrameLedger.Application.Tests.Import;

/// <summary>
/// FR-1.2 (P4 PR-4): discovery joins every store's titles to the ledger — the store's executable where it names one,
/// the locator's guess where it does not, "already in the library" where the row exists, no executable where none
/// is found — and the import adds exactly the ticked rows, hooking off, with the store's facts applied. A store that
/// throws costs its own titles, never the others'.
/// </summary>
public sealed class LibraryImporterTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class ScriptedStore(string platform, params StoreGame[] games) : IStoreLibrarySource
    {
        public bool Throws { get; set; }

        public string Platform => platform;

        public ValueTask<IReadOnlyList<StoreGame>> ListAsync(CancellationToken ct = default) =>
            Throws ? throw new IOException("launcher files unreadable") : ValueTask.FromResult<IReadOnlyList<StoreGame>>(games);
    }

    private sealed class FakeIdentity : IExecutableIdentitySource
    {
        public HashSet<string> Existing { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ExecutableFingerprint? Read(string normalisedExePath) =>
            Existing.Contains(normalisedExePath) ? new ExecutableFingerprint { ExePath = normalisedExePath, SizeBytes = 7, MtimeUnixMs = 7 } : null;

        public string Normalise(string exePath) => exePath.ToUpperInvariant();
    }

    private sealed class FakeLocator : IExecutableLocator
    {
        public Dictionary<string, string?> Guesses { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? PickPrimary(string installDirectory) => Guesses.GetValueOrDefault(installDirectory);
    }

    [Fact]
    public async Task DiscoveryJoinsTheStoresToTheLedgerAndImportAddsOnlyWhatWasTicked()
    {
        var games = new FakeGameRepository();
        var identity = new FakeIdentity();
        var locator = new FakeLocator();
        List<string> log = [];

        var steam = new ScriptedStore("steam",
            new StoreGame("steam", "1091500", "Cyberpunk 2077", @"D:\Steam\steamapps\common\Cyberpunk 2077", null, "15877371"),
            new StoreGame("steam", "2358720", "Wukong", @"D:\Steam\steamapps\common\BlackMythWukong", null, null));
        var gog = new ScriptedStore("gog", new StoreGame("gog", "1207658924", "The Witcher 3", @"D:\GOG\W3", @"D:\GOG\W3\bin\witcher3.exe", "4.04"));
        locator.Guesses[@"D:\Steam\steamapps\common\Cyberpunk 2077"] = @"D:\Steam\steamapps\common\Cyberpunk 2077\bin\x64\Cyberpunk2077.exe";
        identity.Existing.Add(@"D:\STEAM\STEAMAPPS\COMMON\CYBERPUNK 2077\BIN\X64\CYBERPUNK2077.EXE");
        identity.Existing.Add(@"D:\GOG\W3\BIN\WITCHER3.EXE");
        // The Witcher is already in the library, added by hand.
        await games.EnsureAsync(new ExecutableFingerprint { ExePath = @"D:\GOG\W3\BIN\WITCHER3.EXE", SizeBytes = 1, MtimeUnixMs = 1 }, "witcher3", Ct);

        var importer = new LibraryImporter([steam, gog], games, identity, locator, log.Add);
        IReadOnlyList<ImportCandidate> found = await importer.DiscoverAsync(Ct);

        found.Should().HaveCount(3);
        ImportCandidate cp = found.Single(static c => string.Equals(c.Game.StoreId, "1091500", StringComparison.Ordinal));
        cp.ExePath.Should().Be(@"D:\STEAM\STEAMAPPS\COMMON\CYBERPUNK 2077\BIN\X64\CYBERPUNK2077.EXE", "the locator's guess, normalised");
        cp.ExecutableGuessed.Should().BeTrue();
        cp.CanImport.Should().BeTrue();
        ImportCandidate wukong = found.Single(static c => string.Equals(c.Game.StoreId, "2358720", StringComparison.Ordinal));
        wukong.ExePath.Should().BeNull("nothing found under its install directory");
        wukong.CanImport.Should().BeFalse();
        ImportCandidate w3 = found.Single(static c => string.Equals(c.Game.StoreId, "1207658924", StringComparison.Ordinal));
        w3.ExecutableGuessed.Should().BeFalse("GOG named it");
        w3.AlreadyInLibrary.Should().BeTrue();
        w3.CanImport.Should().BeFalse();

        ImportReport report = await importer.ImportAsync([cp, w3], Ct);
        report.Added.Should().Be(1);
        report.Skipped.Should().Be(1, "the Witcher was already there — left as it was, its store facts applied");
        GameRow added = games.Rows[cp.ExePath!];
        added.Name.Should().Be("Cyberpunk 2077", "the store's name, not the exe's");
        added.HookEnabled.Should().BeFalse("import never enables hooking — FR-1.2");
        added.Platform.Should().Be("steam");
        added.StoreId.Should().Be("1091500");
        added.GameVersion.Should().Be("15877371");
        games.Stores.Should().HaveCount(2);
        games.Rows.Should().HaveCount(2);
        log.Should().Contain(static l => l.Contains("added Cyberpunk 2077", StringComparison.Ordinal) && l.Contains("hooking off", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AStoreThatThrowsCostsItsOwnTitlesAndTheSameExecutableUnderTwoStoresIsOneRow()
    {
        var games = new FakeGameRepository();
        var identity = new FakeIdentity();
        var locator = new FakeLocator();
        List<string> log = [];
        var epic = new ScriptedStore("epic", new StoreGame("epic", "e1", "Shared", @"D:\G\Shared", @"D:\G\Shared\game.exe", null)) { Throws = true };
        var gog = new ScriptedStore("gog", new StoreGame("gog", "g1", "Shared", @"D:\G\Shared", @"D:\G\Shared\game.exe", "1.0"));
        var itch = new ScriptedStore("itch", new StoreGame("itch", "i1", "Shared again", @"D:\G\Shared", @"D:\G\Shared\GAME.EXE", null));

        IReadOnlyList<ImportCandidate> found = await new LibraryImporter([epic, gog, itch], games, identity, locator, log.Add).DiscoverAsync(Ct);

        found.Should().ContainSingle().Which.Game.Platform.Should().Be("gog", "epic threw and itch named the same executable, so the first store to name it keeps it");
        log.Should().Contain(static l => l.StartsWith("import: epic library unreadable", StringComparison.Ordinal));

        ImportReport report = await new LibraryImporter([gog], games, identity, locator, log.Add).ImportAsync(found, Ct);
        report.Added.Should().Be(0);
        report.Skipped.Should().Be(1, "the executable is not on disk, so nothing can be fingerprinted or added");
        games.Rows.Should().BeEmpty();
    }
}
