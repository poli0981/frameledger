// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using System.Windows.Media;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Application.Art;
using FrameLedger.Application.Import;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Settings;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.23 (owner decisions D60/D61): a game's artwork — the user's own image first, else what the app of the store that
/// installed it keeps on this PC, found through that store's own records as Play finds it — and the colour it gives the band.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class GameArtTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-gameart-" + Guid.NewGuid().ToString("N"));

    public GameArtTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Install => Path.Combine(_dir, "Steam", "steamapps", "common", "Alpha");

    private GameArt Art(MemorySettings settings, IStoreLibrarySource store, IStoreArtCache cache) =>
        new(new RegisteredSettings(settings), new StoreRecordsReader([store]), [cache], new UiMode(false, Path.Combine(_dir, "data")));

    private (IStoreLibrarySource Store, TestImages.ArtCache Cache) SteamWith(bool cover = true, bool hero = true)
    {
        string? c = cover ? TestImages.Write(Path.Combine(_dir, "cache", "library_600x900.jpg"), TestImages.Png(30, 45, Color.FromRgb(200, 40, 40))) : null;
        string? h = hero ? TestImages.Write(Path.Combine(_dir, "cache", "library_hero.jpg"), TestImages.Png(96, 31, Color.FromRgb(40, 40, 200))) : null;
        var store = new TestImages.Store("steam", new StoreGame("steam", "374320", "Alpha", Install, null, null));
        return (store, new TestImages.ArtCache("steam", "374320", new GameArtFiles(c, h, null)));
    }

    [Fact]
    public async Task TheStoresCoverAndBannerShowWithTheColourOfTheCover()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha", Path.Combine(Install, "Alpha.exe"));
        (IStoreLibrarySource store, TestImages.ArtCache cache) = SteamWith();

        GameArtView view = (await Art(new MemorySettings(), store, cache).ForAsync(game, Ct))!;

        view.Cover.Should().NotBeNull();
        view.Hero.Should().NotBeNull();
        view.CoverIsOwn.Should().BeFalse();
        view.Source.Should().Be(Strings.Art_Source_Steam);
        view.Tint.Should().NotBeNull("the cover is red; the band takes it");
        view.Tint!.Value.R.Should().BeGreaterThan(view.Tint.Value.B, "the cover's colour, not the banner's");
        view.Cover!.IsFrozen.Should().BeTrue("decoded off the UI thread, so frozen to be bound on it");
        cache.Asked.Should().Equal("374320");
    }

    [Fact]
    public async Task TheUsersOwnImageComesBeforeTheStoresAndAMissingOneFallsBackToIt()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha", Path.Combine(Install, "Alpha.exe"));
        (IStoreLibrarySource store, TestImages.ArtCache cache) = SteamWith();
        _ = TestImages.Write(Path.Combine(_dir, "data", "covers", "game-" + game.Id + "-5.png"), TestImages.Png(20, 30, Color.FromRgb(20, 160, 60)));
        GameRow own = game with { CoverPath = "covers\\game-" + game.Id + "-5.png" };
        GameArt art = Art(new MemorySettings(), store, cache);

        GameArtView mine = (await art.ForAsync(own, Ct))!;
        mine.CoverIsOwn.Should().BeTrue();
        mine.Source.Should().Be(Strings.Art_Source_Yours);
        mine.StoreCover.Should().NotBeNull("the edit dialog's \"use the store's image\" needs it");
        mine.Tint!.Value.G.Should().BeGreaterThan(mine.Tint.Value.R, "the band takes the user's image's colour");

        GameArtView gone = (await art.ForAsync(game with { CoverPath = "covers\\game-" + game.Id + "-6.png" }, Ct))!;
        gone.CoverIsOwn.Should().BeFalse("a chosen image whose file is gone is the store's again");
        gone.Source.Should().Be(Strings.Art_Source_Steam);
    }

    [Theory]
    [InlineData("C:\\Windows\\Web\\Wallpaper\\img0.jpg")]
    [InlineData("\\\\server\\share\\x.png")]
    [InlineData("covers\\..\\..\\x.png")]
    [InlineData("covers\\anything.png")]
    [InlineData("other\\game-1-5.png")]
    public void ACoverPathThatIsNotOneOfOursIsNeverFollowed(string coverPath)
    {
        // A viewer opens a copy of a ledger that may come from anywhere: a share would have Windows offer this user's credentials.
        _ = TestImages.Write(Path.Combine(_dir, "data", "covers", "anything.png"), TestImages.Png(4, 4, Colors.Gray));
        CoverStore.Resolve(Path.Combine(_dir, "data"), coverPath).Should().BeNull();
    }

    [Fact]
    public async Task AGameNoArtStoreInstalledHasNoneWhateverItsPlatformSays()
    {
        // D59: detection rewrites platform (an itch build that ships Steam's API reads as Steam) while the id stays the
        // importer's; the store is asked of the records, and no Steam folder holds an itch install.
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha", Path.Combine(_dir, "itch", "apps", "alpha", "Alpha.exe"));
        GameRow claimsSteam = game with { Platform = "steam", StoreId = "374320" };
        (IStoreLibrarySource store, TestImages.ArtCache cache) = SteamWith();

        (await Art(new MemorySettings(), store, cache).ForAsync(claimsSteam, Ct)).Should().BeNull();
        cache.Asked.Should().BeEmpty("no Steam record owns the folder, so Steam's artwork is never asked");
    }

    [Fact]
    public async Task OffInSettingsNothingIsReadAndUntintedTheBandKeepsTheCardsFill()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha", Path.Combine(Install, "Alpha.exe"));
        (IStoreLibrarySource store, TestImages.ArtCache cache) = SteamWith();
        var off = new MemorySettings();
        off.Rows[SettingsRegistry.UiGameArt.Key] = "0";
        var noTint = new MemorySettings();
        noTint.Rows[SettingsRegistry.UiGameArtTint.Key] = "0";

        (await Art(off, store, cache).ForAsync(game, Ct)).Should().BeNull();
        ((TestImages.Store)store).Reads.Should().Be(0, "with the artwork off no store's records are read");
        GameArtView untinted = (await Art(noTint, store, cache).ForAsync(game, Ct))!;
        untinted.Cover.Should().NotBeNull();
        untinted.Tint.Should().BeNull();
    }

    [Fact]
    public async Task ADamagedFileIsNoImageAndAGreyPictureTintsNothing()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha", Path.Combine(Install, "Alpha.exe"));
        string damaged = TestImages.Write(Path.Combine(_dir, "cache", "library_600x900.jpg"), [0x42, 0x41, 0x44]);
        string grey = TestImages.Write(Path.Combine(_dir, "cache", "library_hero.jpg"), TestImages.Png(64, 20, Color.FromRgb(128, 128, 128)));
        var store = new TestImages.Store("steam", new StoreGame("steam", "374320", "Alpha", Install, null, null));
        var cache = new TestImages.ArtCache("steam", "374320", new GameArtFiles(damaged, grey, null));

        GameArtView view = (await Art(new MemorySettings(), store, cache).ForAsync(game, Ct))!;

        view.Cover.Should().BeNull("a file Windows cannot read as an image is no image, never an error");
        view.Hero.Should().NotBeNull();
        view.Tint.Should().BeNull("a grey picture has no colour to give");
    }

    [Fact]
    public async Task TheStoresRecordsAreReadOnceForPagesOpenedTogether()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha", Path.Combine(Install, "Alpha.exe"));
        (IStoreLibrarySource store, TestImages.ArtCache cache) = SteamWith();
        GameArt art = Art(new MemorySettings(), store, cache);

        _ = await art.ForAsync(game, Ct);
        _ = await art.ForAsync(game, Ct);

        ((TestImages.Store)store).Reads.Should().Be(1, "Steam's records are kept two minutes, not read at every page");
    }
}
