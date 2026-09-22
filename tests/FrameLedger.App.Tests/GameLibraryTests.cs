using System.IO;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Tests;

/// <summary>The library over a scratch ledger: cards with their aggregates, the detail load, add (hooking off), remove, recent, totals.</summary>
public sealed class GameLibraryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task CardsCarryTheAggregateAndTheDetailCarriesSessionsNewestFirst()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow a = await s.GameAsync("Alpha");
        GameRow b = await s.GameAsync("Beta");
        DateTimeOffset t0 = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);
        await s.SessionAsync(a.Id, t0, seconds: 600);
        await s.SessionAsync(a.Id, t0.AddDays(1), seconds: 300, hooked: false);
        await s.Annotations.UpsertAsync(new SessionAnnotation { SessionId = 1, Tags = ["bench"] }, Ct);

        IReadOnlyList<GameCard> cards = await s.Library.ListCardsAsync(Ct);
        cards.Select(static c => c.Row.Name).Should().Equal("Alpha", "Beta");
        cards[0].Summary!.SessionCount.Should().Be(2);
        cards[0].Summary!.HookedCount.Should().Be(1);
        cards[0].Summary!.TotalSeconds.Should().Be(900);
        cards[1].Summary.Should().BeNull("no sessions yet");

        GameDetail detail = (await s.Library.LoadAsync(a.Id, ct: Ct))!;
        detail.Sessions.Should().HaveCount(2).And.BeInDescendingOrder(static r => r.StartedAt);
        detail.Annotations.Should().ContainKey(1);
        (await s.Library.LoadAsync(b.Id + 99, ct: Ct)).Should().BeNull();
    }

    [Fact]
    public async Task AddingIsHookingOffAndAnUnreadableFileAddsNothing()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        string exe = Path.Combine(Path.GetTempPath(), "fl-app-" + Guid.NewGuid().ToString("N") + ".exe");
        await File.WriteAllBytesAsync(exe, [0x4D, 0x5A, 0, 0], Ct);
        try
        {
            GameRow? added = await s.Library.AddAsync(exe, Ct);
            added.Should().NotBeNull();
            added!.HookEnabled.Should().BeFalse("CLAUDE.md rule 1: adding never enables anything");
            added.Name.Should().Be(Path.GetFileNameWithoutExtension(exe));
            (await s.Library.AddAsync(exe, Ct))!.Id.Should().Be(added.Id, "the same path is the same row");
        }
        finally
        {
            File.Delete(exe);
        }

        (await s.Library.AddAsync(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N") + ".exe"), Ct)).Should().BeNull();
    }

    /// <summary>
    /// A hand-added <c>Game.exe</c> is named after its game (2026-09-23): RPG Maker's own title when the folder carries one,
    /// else the nearest folder that is not a runtime folder — never "Game". <i>HELLO, HELLO WORLD!</i>'s runtime lives in
    /// <c>swiftshader\</c>, a folder below its title.
    /// </summary>
    [Fact]
    public async Task AGenericExecutableIsNamedAfterItsGame()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        string root = Path.Combine(Path.GetTempPath(), "fl-app-name-" + Guid.NewGuid().ToString("N"));
        string titled = Path.Combine(root, "hello-hello-world", "HELLO, HELLO WORLD!", "swiftshader", "Game.exe");
        string untitled = Path.Combine(root, "Some Game", "Game.exe");
        Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(titled)!, "www", "data"));
        Directory.CreateDirectory(Path.GetDirectoryName(untitled)!);
        await File.WriteAllBytesAsync(titled, [0x4D, 0x5A, 0, 0], Ct);
        await File.WriteAllBytesAsync(untitled, [0x4D, 0x5A, 0, 0], Ct);
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(titled)!, "www", "data", "System.json"), "{\"gameTitle\":\"HELLO, HELLO WORLD!\"}", Ct);
        try
        {
            (await s.Library.AddAsync(titled, Ct))!.Name.Should().Be("HELLO, HELLO WORLD!", "the engine's own title");
            (await s.Library.AddAsync(untitled, Ct))!.Name.Should().Be("Some Game", "the folder, when there is no title to read");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RecentAndTotalsAcrossGames()
    {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);
        await using ScratchLedger s = await ScratchLedger.OpenAsync(new FixedClock(now));
        GameRow a = await s.GameAsync("Alpha");
        GameRow b = await s.GameAsync("Beta");
        await s.SessionAsync(a.Id, now.AddDays(-10), seconds: 600);
        await s.SessionAsync(b.Id, now.AddDays(-2), seconds: 1200);
        await s.SessionAsync(a.Id, now.AddHours(-1), seconds: 60);

        IReadOnlyList<RecentSession> recent = await s.Library.RecentAsync(2, Ct);
        recent.Select(static r => r.GameName).Should().Equal("Alpha", "Beta");

        LibraryTotals totals = await s.Library.TotalsAsync(Ct);
        totals.GamesTracked.Should().Be(2);
        totals.TotalSeconds.Should().Be(1860);
        totals.SessionsThisWeek.Should().Be(2, "the one ten days ago is not this week");

        (await s.Library.RemoveAsync(b.Id, keepSessions: true, Ct)).Should().BeTrue();
        (await s.Library.TotalsAsync(Ct)).GamesTracked.Should().Be(1, "removed from the library");
        (await s.Library.RecentAsync(5, Ct)).Should().HaveCount(3, "kept sessions stay readable");
    }
}
