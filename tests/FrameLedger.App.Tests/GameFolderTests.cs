// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using FluentAssertions;
using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>
/// Where Change executable… opens (beta.19, owner request 2026-10-12): the game's root folder for each layout the library
/// holds — a store's install folder, an Unreal package outside a store, a loose folder — the nearest folder still there for
/// an entry whose folder is gone, and nothing for a drive that is not connected.
/// </summary>
public sealed class GameFolderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-gamefolder-" + Guid.NewGuid().ToString("N"));

    public GameFolderTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Sub(params string[] parts)
    {
        string p = Path.Combine([_dir, .. parts]);
        Directory.CreateDirectory(p);
        return p;
    }

    [Fact]
    public void AStoreInstallOpensAtItsFolderUnderTheStoresBoundary()
    {
        string install = Sub("SteamLibrary", "steamapps", "common", "Lies of P");
        string bin = Sub("SteamLibrary", "steamapps", "common", "Lies of P", "LiesofP", "Binaries", "Win64");

        GameFolder.StartFolder(Path.Combine(bin, "LiesofP-Win64-Shipping.exe")).Should().Be(install);
    }

    [Fact]
    public void AnUnrealPackageOutsideAStoreOpensAboveItsProjectFolder()
    {
        string root = Sub("Games", "Hell Is Us");
        string bin = Sub("Games", "Hell Is Us", "HellIsUs", "Binaries", "Win64");

        GameFolder.StartFolder(Path.Combine(bin, "HellIsUs-Win64-Shipping.exe")).Should().Be(root, "<root>\\<Project>\\Binaries\\Win64 is Unreal's own layout");
    }

    [Fact]
    public void ALooseFolderOpensWhereTheExecutableIs()
    {
        string game = Sub("it", "a-dolls-locket-remake", "Dolls Locket");

        GameFolder.StartFolder(Path.Combine(game, "notification_helper.exe")).Should().Be(game);
    }

    [Fact]
    public void AFolderThatIsGoneOpensAtTheNearestOneStillThere()
    {
        string library = Sub("it");

        GameFolder.StartFolder(Path.Combine(library, "uninstalled", "Sub", "Game.exe")).Should().Be(library, "the entry reads Not installed; its library folder is still there");
    }

    [Fact]
    public void ADriveThatIsNotConnectedOpensNowhereInParticular()
    {
        char? absent = null;
        for (char letter = 'Z'; letter >= 'D'; letter--)
        {
            if (!Directory.Exists(letter + @":\"))
            {
                absent = letter;
                break;
            }
        }

        Assert.SkipWhen(absent is null, "every drive letter from D to Z is in use on this machine");
        GameFolder.StartFolder(absent + @":\Games\Title\Title.exe").Should().BeNull("Windows then opens where the last pick was");
    }
}
