// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.23 (owner decision D60): the images users choose for their games — a re-encoded copy in the data folder's
/// <c>covers</c>, a new name for each choice — and the tidying that deletes the ones no row names any more.
/// </summary>
public sealed class CoverStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-covers-" + Guid.NewGuid().ToString("N"));

    public CoverStoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        // A junction a test made goes on its own first: met inside a recursive delete, it is unmounted as a volume would be,
        // which Windows refuses without elevation (UnauthorizedAccessException) — though the link itself is then removed.
        var covers = new DirectoryInfo(Path.Combine(Data, "covers"));
        if (covers.Exists && covers.LinkTarget is not null)
        {
            covers.Delete();
        }

        Directory.Delete(_dir, recursive: true);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Data => Path.Combine(_dir, "data");

    [Fact]
    public async Task APickedImageIsCopiedAsAPngNoLargerThan1200PixelsUnderANewName()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var store = new CoverStore(new UiMode(false, Data), s.Games);
        string picked = TestImages.Write(Path.Combine(_dir, "mine.png"), TestImages.Png(1600, 2400, Color.FromRgb(10, 120, 200)));

        string first = (await store.ImportAsync(7, picked, Ct))!;
        string second = (await store.ImportAsync(7, picked, Ct))!;

        first.Should().MatchRegex(@"^covers\\game-7-\d+\.png$");
        second.Should().NotBe(first, "each choice is a new file, so a shown image is never written over");
        string full = CoverStore.Resolve(Data, first)!;
        BitmapFrame copy = BitmapDecoder.Create(new Uri(full), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        copy.PixelHeight.Should().Be(1200);
        copy.PixelWidth.Should().Be(800);
        Directory.EnumerateFiles(Path.Combine(Data, "covers"), "*.tmp").Should().BeEmpty();
        File.Exists(picked).Should().BeTrue("the user's own file is read, never moved");
    }

    [Fact]
    public async Task AFileThatIsNotAnImageIsRefusedAndLeavesNothing()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var store = new CoverStore(new UiMode(false, Data), s.Games);
        string notAnImage = TestImages.Write(Path.Combine(_dir, "notes.png"), "not an image"u8.ToArray());

        (await store.ImportAsync(7, notAnImage, Ct)).Should().BeNull();
        (Directory.Exists(Path.Combine(Data, "covers")) ? Directory.EnumerateFiles(Path.Combine(Data, "covers")) : []).Should().BeEmpty();
    }

    [Fact]
    public async Task TidyingDeletesWhatNoRowNamesAndKeepsWhatARemovedRowStillOwns()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow kept = await s.GameAsync("Kept");
        GameRow removed = await s.GameAsync("Removed");
        string covers = Path.Combine(Data, "covers");
        string keptFile = "game-" + kept.Id + "-100.png";
        string removedFile = "game-" + removed.Id + "-100.png";
        foreach (string name in new[] { keptFile, removedFile, "game-" + kept.Id + "-99.png", "game-999999-1.png", "readme.txt" })
        {
            _ = TestImages.Write(Path.Combine(covers, name), [1]);
        }

        string stale = TestImages.Write(Path.Combine(covers, "game-" + kept.Id + "-98.png.tmp"), [1]);
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-2));
        string fresh = TestImages.Write(Path.Combine(covers, "game-" + kept.Id + "-101.png.tmp"), [1]);
        await s.Games.UpdateMetadataAsync(kept.Id, GameMetadata.Of(kept) with { CoverPath = "covers\\" + keptFile }, Ct);
        await s.Games.UpdateMetadataAsync(removed.Id, GameMetadata.Of(removed) with { CoverPath = "covers\\" + removedFile }, Ct);
        (await s.Games.RemoveAsync(removed.Id, keepSessions: true, Ct)).Should().BeTrue();

        int deleted = await new CoverStore(new UiMode(false, Data), s.Games).PruneAsync(Ct);

        deleted.Should().Be(3, "the replaced image, the deleted game's, and the copy half-written two hours ago");
        Directory.EnumerateFiles(covers).Select(Path.GetFileName).Should().BeEquivalentTo([keptFile, removedFile, "readme.txt", Path.GetFileName(fresh)],
            "a row removed with its sessions kept comes back with its image; another file's name is not ours to touch");
    }

    [Fact]
    public async Task DeletingFollowsOnlyOurOwnNames()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var store = new CoverStore(new UiMode(false, Data), s.Games);
        string outside = TestImages.Write(Path.Combine(_dir, "precious.png"), [1]);
        string ours = TestImages.Write(Path.Combine(Data, "covers", "game-3-4.png"), [1]);

        store.Delete("covers\\..\\..\\precious.png");
        store.Delete(outside);
        store.Delete("covers\\game-3-4.png");

        File.Exists(outside).Should().BeTrue();
        File.Exists(ours).Should().BeFalse();
    }

    /// <summary>A <c>covers</c> folder that is a junction leads to another place: nothing behind it is shown, deleted, written or tidied.</summary>
    [Fact]
    public async Task ACoversFolderThatIsALinkIsNeverFollowed()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        string elsewhere = Path.Combine(_dir, "elsewhere");
        string there = TestImages.Write(Path.Combine(elsewhere, "game-" + game.Id + "-1.png"), TestImages.Png(4, 4, Color.FromRgb(200, 30, 30)));
        _ = Directory.CreateDirectory(Data);
        Junction(Path.Combine(Data, "covers"), elsewhere);
        var store = new CoverStore(new UiMode(false, Data), s.Games);
        string picked = TestImages.Write(Path.Combine(_dir, "mine.png"), TestImages.Png(4, 4, Color.FromRgb(10, 120, 200)));

        CoverStore.Resolve(Data, "covers\\" + Path.GetFileName(there)).Should().BeNull("the folder is a link");
        store.Delete("covers\\" + Path.GetFileName(there));
        (await store.ImportAsync(game.Id, picked, Ct)).Should().BeNull("no image is written through a link");
        (await store.PruneAsync(Ct)).Should().Be(0);

        Directory.EnumerateFiles(elsewhere).Should().BeEquivalentTo([there], "nothing behind the link was deleted or written");
    }

    private static void Junction(string link, string target)
    {
        var start = new ProcessStartInfo("cmd.exe") { CreateNoWindow = true, UseShellExecute = false };
        foreach (string arg in new[] { "/c", "mklink", "/J", link, target })
        {
            start.ArgumentList.Add(arg);
        }

        using Process mklink = Process.Start(start)!;
        mklink.WaitForExit();
        mklink.ExitCode.Should().Be(0, "a junction needs no privilege");
    }
}
