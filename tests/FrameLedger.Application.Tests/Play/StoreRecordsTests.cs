// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Import;
using FrameLedger.Application.Play;

namespace FrameLedger.Application.Tests.Play;

/// <summary>beta.21 (Play): the store that installed a game is the one whose own record names the folder its executable is in.</summary>
public sealed class StoreRecordsTests
{
    [Fact]
    public void TheRecordWhoseFolderHoldsTheExecutableIsTheGamesStore()
    {
        StoreGame wuwa = new("steam", "3513350", "Wuthering Waves", @"D:\SteamLibrary\steamapps\common\Wuthering Waves", null, null);
        StoreGame other = new("steam", "1091500", "Cyberpunk 2077", @"D:\SteamLibrary\steamapps\common\Cyberpunk 2077", null, null);

        StoreRecords.Owning(@"D:\SteamLibrary\steamapps\common\Wuthering Waves\Client\Binaries\Win64\Client-Win64-Shipping.exe", "3513350", [other, wuwa])
            .Should().BeSameAs(wuwa);
        StoreRecords.Owning(@"d:\steamlibrary\STEAMAPPS\common\wuthering waves\Client-Win64-Shipping.exe", null, [wuwa])
            .Should().BeSameAs(wuwa, "Windows paths are not case-sensitive, and an entry the user added has no store id");
    }

    [Fact]
    public void AFolderThatOnlySharesAPrefixIsNotTheGames()
    {
        StoreGame half = new("steam", "1", "Half", @"D:\Games\Half", null, null);

        StoreRecords.Owning(@"D:\Games\Half-Life 2\hl2.exe", null, [half]).Should().BeNull("`Half` is not the folder of `Half-Life 2`");
        StoreRecords.Owning(@"D:\Games\Elsewhere\game.exe", null, [half]).Should().BeNull("no store installed it");
    }

    [Fact]
    public void TheDeepestFolderWinsAndThenTheEntrysOwnStoreId()
    {
        StoreGame outer = new("itch", "10", "Bundle", @"D:\Games\Bundle", null, null);
        StoreGame inner = new("gog", "20", "Game", @"D:\Games\Bundle\Game", null, null);
        StoreRecords.Owning(@"D:\Games\Bundle\Game\game.exe", null, [outer, inner]).Should().BeSameAs(inner);

        StoreGame steam = new("steam", "30", "Twin", @"D:\Games\Twin", null, null);
        StoreGame itch = new("itch", "40", "Twin", @"D:\Games\Twin\", null, null);
        StoreRecords.Owning(@"D:\Games\Twin\twin.exe", "40", [steam, itch]).Should().BeSameAs(itch, "two stores name one folder: the entry's own id decides");
        StoreRecords.Owning(@"D:\Games\Twin\twin.exe", "30", [itch, steam]).Should().BeSameAs(steam);
    }

    /// <summary>
    /// The case the entry's platform column cannot settle: detection rewrites a platform it finds (an itch build that ships
    /// Steam's API reads as Steam) while the store id stays the importer's — a Steam link built from that id would start some
    /// other game. The records decide, not the column.
    /// </summary>
    [Fact]
    public void AnItchBuildThatShipsSteamsApiIsStillItchs()
    {
        StoreGame itch = new("itch", "5015466", "Some Game", @"D:\another\it\some-game", null, null);

        StoreGame? owner = StoreRecords.Owning(@"D:\another\it\some-game\Game\Game.exe", "5015466", [itch]);

        owner.Should().BeSameAs(itch);
        StoreLaunches.For(owner!, null)!.Target.Should().StartWith("itch://");
    }
}
