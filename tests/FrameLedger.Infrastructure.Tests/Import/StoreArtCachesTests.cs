// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Art;
using FrameLedger.Infrastructure.Art;

namespace FrameLedger.Infrastructure.Tests.Import;

/// <summary>
/// beta.23 (owner decision D60): the artwork the Steam and GOG GALAXY apps keep on this PC, found in the layouts read on the
/// owner's PC on 2026-10-14 — Steam's files directly in the app's folder, in hash-named folders, or both; GOG's per user
/// and per product — and never outside the store's own folder.
/// </summary>
public sealed class StoreArtCachesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-art-" + Guid.NewGuid().ToString("N"));

    public StoreArtCachesTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Touch(DateTime writtenUtc, params string[] parts)
    {
        string path = Path.Combine([_dir, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [1, 2, 3]);
        File.SetLastWriteTimeUtc(path, writtenUtc);
        return path;
    }

    private string Steam(string appId, params string[] parts) => Touch(DateTime.UtcNow, ["Steam", "appcache", "librarycache", appId, .. parts]);

    [Fact]
    public void SteamFindsTheFilesInTheAppsFolder()
    {
        string cover = Steam("374320", "library_600x900.jpg");
        string hero = Steam("374320", "library_hero.jpg");
        string header = Steam("374320", "header.jpg");
        _ = Steam("374320", "library_hero_blur.jpg");
        _ = Steam("374320", "logo.png");

        GameArtFiles files = new SteamArtCache(Path.Combine(_dir, "Steam")).Find("374320");

        files.Should().Be(new GameArtFiles(cover, hero, header));
    }

    [Fact]
    public void SteamFindsTheFilesInHashNamedFoldersAndPrefersTheBetterCoverAndTheNewestCopy()
    {
        DateTime old = DateTime.UtcNow.AddDays(-30);
        _ = Touch(old, "Steam", "appcache", "librarycache", "1091500", "aaaa", "library_hero.jpg");
        string newerHero = Steam("1091500", "bbbb", "library_hero.jpg");
        string capsule = Steam("1091500", "cccc", "library_capsule.jpg");
        string header = Steam("1091500", "dddd", "library_header.jpg");

        GameArtFiles files = new SteamArtCache(Path.Combine(_dir, "Steam")).Find("1091500");

        files.Cover.Should().Be(capsule);
        files.Hero.Should().Be(newerHero, "an asset Steam replaced has a newer copy in a new folder");
        files.Header.Should().Be(header);

        string portrait = Steam("1091500", "library_600x900.jpg");
        new SteamArtCache(Path.Combine(_dir, "Steam")).Find("1091500").Cover.Should().Be(portrait, "the 600x900 portrait is preferred over the capsule");
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("..\\..\\Windows")]
    [InlineData("C:\\")]
    [InlineData("12a")]
    public void AnIdThatIsNotTheStoresOwnShapeNamesNoFolder(string id)
    {
        _ = Steam("374320", "library_600x900.jpg");
        new SteamArtCache(Path.Combine(_dir, "Steam")).Find(id).Should().Be(GameArtFiles.None);
        new GogArtCache(Path.Combine(_dir, "webcache")).Find(id).Should().Be(GameArtFiles.None);
    }

    [Fact]
    public void NothingThereIsNoneNeverAnError()
    {
        new SteamArtCache(Path.Combine(_dir, "no-steam")).Find("374320").Should().Be(GameArtFiles.None);
        new GogArtCache(Path.Combine(_dir, "no-galaxy")).Find("1125815775").Should().Be(GameArtFiles.None);
        _ = Directory.CreateDirectory(Path.Combine(_dir, "Steam", "appcache", "librarycache", "220"));
        new SteamArtCache(Path.Combine(_dir, "Steam")).Find("220").IsEmpty.Should().BeTrue("Steam keeps a folder with no artwork in it for some apps");
    }

    [Fact]
    public void GogFindsTheCoverAndTheBannerInAnyUsersFolder()
    {
        _ = Touch(DateTime.UtcNow.AddDays(-2), "webcache", "111", "gog", "1125815775", "00aa_glx_vertical_cover.webp");
        string cover = Touch(DateTime.UtcNow, "webcache", "222", "gog", "1125815775", "11bb_glx_vertical_cover.webp");
        string banner = Touch(DateTime.UtcNow, "webcache", "222", "gog", "1125815775", "22cc_glx_bg_top_padding_7.webp");
        _ = Touch(DateTime.UtcNow, "webcache", "222", "gog", "1125815775", "33dd_glx_square_icon_v2.webp");
        _ = Directory.CreateDirectory(Path.Combine(_dir, "webcache", "common"));

        GameArtFiles files = new GogArtCache(Path.Combine(_dir, "webcache")).Find("1125815775");

        files.Should().Be(new GameArtFiles(cover, banner, null), "the newest copy wins across users, and GOG keeps no header");
    }
}
