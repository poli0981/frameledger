// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.IO;
using FluentAssertions;
using FrameLedger.App.Controls;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.11 (owner requests 2026-10-03): the library page counts its games, reloads itself while on screen when a session
/// ends or an import adds games, lets go when it leaves, and its grid's columns fill the width. In the culture collection
/// (2026-10-03): a card's tooltip is resource text compared with the resource read again, and a class flipping
/// <c>Strings.Culture</c> in parallel between the two reads failed it once in a local run.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class GamesViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class NoNavigation : IPageNavigator
    {
        public void Navigate<TPage>()
            where TPage : class
        {
        }

        public void GoBack()
        {
        }
    }

    private sealed class NoPicker : IGamePicker
    {
        public string? PickExecutable(string? initialDirectory = null) => null;
    }

    private sealed class NoStrip : IMessageStrip
    {
        public void Info(string title, string body)
        {
        }

        public void Success(string title, string body)
        {
        }

        public void Warn(string title, string body)
        {
        }
    }

    [Fact]
    public async Task ThePageOnScreenReloadsWhenAnImportAddsGamesOrASessionEndsAndNotAfterItLeaves()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow alpha = await s.GameAsync("Alpha");
        var selection = new GameSelection();
        var nav = new NoNavigation();
        var link = new FakeAgentLink();
        var changes = new LibraryChanges();
        var vm = new GamesViewModel(s.Library, selection, nav, new AddGameFlow(s.Library, new NoPicker(), selection, nav, new NoStrip()), agent: link, changes: changes);
        Task loaded = vm.Pending;
        await loaded;
        vm.HeaderText.Should().EndWith("1");
        vm.Attach();
        vm.Attach();    // a second Loaded does not subscribe twice

        _ = await s.GameAsync("Bravo");
        changes.Announce();
        Task afterImport = vm.Pending;
        await afterImport;
        vm.Games.Should().HaveCount(2, "an import made from the menu while the page is on screen");
        vm.HeaderText.Should().EndWith("2");

        long session = await s.SessionAsync(alpha.Id, DateTimeOffset.UtcNow);
        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(Guid.NewGuid(), session, "normal", 1, "saved", "exited", alpha.Id, "Alpha"));
        Task afterSession = vm.Pending;
        await afterSession;
        vm.Games.Single(static g => string.Equals(g.Name, "Alpha", StringComparison.Ordinal)).SessionCount.Should().Be(1);

        vm.Detach();
        Task before = vm.Pending;
        _ = await s.GameAsync("Charlie");
        changes.Announce();
        vm.Pending.Should().BeSameAs(before, "a page that left holds nothing and reloads nothing");
        vm.Games.Should().HaveCount(2);
    }

    private sealed class RecordingStrip : IMessageStrip
    {
        public List<string> Lines { get; } = [];

        public void Info(string title, string body) => Lines.Add(title + ": " + body);

        public void Success(string title, string body) => Lines.Add(title + ": " + body);

        public void Warn(string title, string body) => Lines.Add(title + ": " + body);
    }

    /// <summary>A clock whose timers fire when the test says so (beta.19): the page's wait for the rest of a check, held.</summary>
    private sealed class HeldTimers : TimeProvider
    {
        private readonly List<(TimerCallback Callback, object? State)> _due = [];

        public int Waiting => _due.Count;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _due.Add((callback, state));
            return new Held();
        }

        public void Fire()
        {
            (TimerCallback Callback, object? State)[] due = [.. _due];
            _due.Clear();
            foreach ((TimerCallback callback, object? state) in due)
            {
                callback(state);
            }
        }

        private sealed class Held : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// beta.19, D55: the Agent removed an entry whose game is no longer installed — the page on screen drops its card and says
    /// why, in words that name the switch that turns it off, once it has waited a moment for the rest of the same check.
    /// </summary>
    [Fact]
    public async Task AnEntryTheAgentRemovedLeavesThePageOnScreenWithAWord()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow gone = await s.GameAsync("Gone");
            _ = await s.GameAsync("Kept");
            var selection = new GameSelection();
            var nav = new NoNavigation();
            var link = new FakeAgentLink();
            var strip = new RecordingStrip();
            var clock = new HeldTimers();
            var vm = new GamesViewModel(s.Library, selection, nav, new AddGameFlow(s.Library, new NoPicker(), selection, nav, new NoStrip()), agent: link, strip: strip, clock: clock);
            Task loaded = vm.Pending;
            await loaded;
            vm.Attach();

            // What the Agent did, then said.
            await s.Db.WriteAsync((c, tx, ct) => Dapper.SqlMapper.ExecuteAsync(c, new Dapper.CommandDefinition("DELETE FROM games WHERE id = @id", new { id = gone.Id }, tx, cancellationToken: ct)), Ct);
            link.Raise(IpcMessageType.GameRemoved, new GameRemovedEvent(gone.Id, "Gone", gone.Fingerprint.ExePath, GameRemovedEvent.Uninstalled));
            Task said = vm.Pending;
            strip.Lines.Should().BeEmpty("the page waits a moment for the rest of the same check");
            clock.Fire();
            await said;

            vm.Games.Should().ContainSingle().Which.Name.Should().Be("Kept");
            strip.Lines.Should().ContainSingle().Which.Should().StartWith("Removed from the library: Gone is no longer installed").And.Contain("Settings ▸ Data");
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    /// <summary>
    /// beta.19: a check that removes several entries says each in its own event, milliseconds apart. The page read the library
    /// and spoke once per event, and the shell's one snackbar kept only the last name; it now waits once, reads the library
    /// once and says them all in one notice — the count, and five names at most.
    /// </summary>
    [Fact]
    public async Task OneCheckThatRemovesSeveralEntriesIsSaidOnceAfterOneRead()
    {
        CultureInfo? previous = Strings.Culture;
        Strings.Culture = CultureInfo.GetCultureInfo("en");
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            GameRow[] gone = [await s.GameAsync("Alpha"), await s.GameAsync("Bravo"), await s.GameAsync("Charlie")];
            _ = await s.GameAsync("Kept");
            var selection = new GameSelection();
            var nav = new NoNavigation();
            var link = new FakeAgentLink();
            var strip = new RecordingStrip();
            var clock = new HeldTimers();
            var vm = new GamesViewModel(s.Library, selection, nav, new AddGameFlow(s.Library, new NoPicker(), selection, nav, new NoStrip()), agent: link, strip: strip, clock: clock);
            Task loaded = vm.Pending;
            await loaded;
            vm.Attach();

            foreach (GameRow row in gone)
            {
                await s.Db.WriteAsync((c, tx, ct) => Dapper.SqlMapper.ExecuteAsync(c, new Dapper.CommandDefinition("DELETE FROM games WHERE id = @id", new { id = row.Id }, tx, cancellationToken: ct)), Ct);
            }

            foreach (GameRow row in gone)
            {
                link.Raise(IpcMessageType.GameRemoved, new GameRemovedEvent(row.Id, row.Name, row.Fingerprint.ExePath, GameRemovedEvent.Uninstalled));
            }

            Task said = vm.Pending;
            clock.Waiting.Should().Be(1, "one wait for the whole check, not one per entry");
            clock.Fire();
            await said;

            vm.Games.Should().ContainSingle().Which.Name.Should().Be("Kept");
            strip.Lines.Should().ContainSingle().Which.Should().Be("Removed from the library: 3 games are no longer installed and had no sessions, so the capture "
                + "agent removed them: Alpha, Bravo, Charlie. Settings ▸ Data turns this off.");

            // Six in one check: the count, five names and how many more.
            for (int i = 1; i <= 6; i++)
            {
                link.Raise(IpcMessageType.GameRemoved, new GameRemovedEvent(1000 + i, "G" + i.ToString(CultureInfo.InvariantCulture), @"Q:\G\g.exe", GameRemovedEvent.Uninstalled));
            }

            Task again = vm.Pending;
            clock.Waiting.Should().Be(1);
            clock.Fire();
            await again;

            strip.Lines.Should().HaveCount(2);
            strip.Lines[^1].Should().Contain("6 games").And.Contain(": G1, G2, G3, G4, G5 and 1 more. ").And.NotContain("G6");
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    [Fact]
    public async Task ACardSaysWhetherItsExecutableIsThereAndWhy()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow row = await s.GameAsync("Gone", @"Q:\Games\Gone\gone.exe");

        new GameCardViewModel(new GameCard(row, null)).NotInstalled.Should().BeFalse("installed unless the listing said otherwise");
        var missing = new GameCardViewModel(new GameCard(row, null), installed: false);
        missing.NotInstalled.Should().BeTrue();
        missing.NotInstalledToolTip.Should().Be(Formats.ExecutableMissingText(@"Q:\Games\Gone\gone.exe"), "the game page's own words for why");
        missing.StatusText.Should().Contain(Strings.Games_Card_NotInstalled);

        await using ScratchLedger s2 = await ScratchLedger.OpenAsync();
        var listed = new GamesViewModel(s2.Library, new GameSelection(), new NoNavigation(),
            new AddGameFlow(s2.Library, new NoPicker(), new GameSelection(), new NoNavigation(), new NoStrip()));
        string here = typeof(GamesViewModelTests).Assembly.Location;
        _ = await s2.GameAsync("Here", here);
        _ = await s2.GameAsync("Gone", Path.Combine(AppContext.BaseDirectory, "no-such-game.exe"));
        await listed.LoadAsync(Ct);
        listed.Games.Single(static g => string.Equals(g.Name, "Here", StringComparison.Ordinal)).NotInstalled.Should().BeFalse();
        listed.Games.Single(static g => string.Equals(g.Name, "Gone", StringComparison.Ordinal)).NotInstalled.Should().BeTrue();
    }

    [Theory]
    [InlineData(1200, 3)]
    [InlineData(1248, 4)]
    [InlineData(1235, 3)]
    [InlineData(624, 2)]
    [InlineData(250, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    public void TheGridHasAsManyColumnsAsFitAtTheMinimumWidth(double width, int columns)
    {
        UniformWrapPanel.ColumnsFor(width, 300, 12).Should().Be(columns);
        if (!double.IsInfinity(width))
        {
            double item = UniformWrapPanel.ItemWidthFor(width, columns, 12);
            item.Should().BeGreaterThanOrEqualTo(columns == 1 ? 0 : 300);
            ((columns * item) + ((columns - 1) * 12)).Should().BeApproximately(width, 0.001, "a row fills the width");
        }
    }
    /// <summary>beta.15: the sort order is labelled in the language the page opened in.</summary>
    [Fact]
    public async Task TheSortListIsInTheLanguageThePageOpenedIn()
    {
        CultureInfo? previous = Strings.Culture;
        try
        {
            await using ScratchLedger s = await ScratchLedger.OpenAsync();
            var selection = new GameSelection();
            var nav = new NoNavigation();
            GamesViewModel Open() => new(s.Library, selection, nav, new AddGameFlow(s.Library, new NoPicker(), selection, nav, new NoStrip()));
            Strings.Culture = CultureInfo.GetCultureInfo("en");
            GamesViewModel english = Open();
            string englishName = Strings.Games_Sort_Name;
            Strings.Culture = CultureInfo.GetCultureInfo("vi");
            GamesViewModel vietnamese = Open();
            await english.Pending;
            await vietnamese.Pending;

            english.Sorts[0].Label.Should().Be(englishName);
            vietnamese.Sorts[0].Label.Should().Be(Strings.Games_Sort_Name).And.NotBe(englishName);
        }
        finally
        {
            Strings.Culture = previous;
        }
    }
}
