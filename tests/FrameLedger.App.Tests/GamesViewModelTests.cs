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
/// ends or an import adds games, lets go when it leaves, and its grid's columns fill the width.
/// </summary>
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
        public string? PickExecutable() => null;
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

    [Fact]
    public async Task ACardSaysWhetherItsExecutableIsThereAndWhy()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow row = await s.GameAsync("Gone", @"Q:\Games\Gone\gone.exe");

        new GameCardViewModel(new GameCard(row, null)).NotInstalled.Should().BeFalse("present unless the listing said otherwise");
        var missing = new GameCardViewModel(new GameCard(row, null), ExecutablePresence.Missing);
        missing.NotInstalled.Should().BeTrue();
        missing.NotInstalledToolTip.Should().Contain(@"Q:\Games\Gone\gone.exe");
        missing.StatusText.Should().Contain(Strings.Games_Card_NotInstalled);
        var drive = new GameCardViewModel(new GameCard(row, null), ExecutablePresence.DriveMissing);
        drive.NotInstalledToolTip.Should().Contain(@"Q:\");

        string here = Path.Combine(AppContext.BaseDirectory, Path.GetFileName(typeof(GamesViewModelTests).Assembly.Location));
        ExecutablePresenceProbe.Of(here).Should().Be(ExecutablePresence.Present);
        ExecutablePresenceProbe.Of(Path.Combine(AppContext.BaseDirectory, "no-such-game.exe")).Should().Be(ExecutablePresence.Missing);
        ExecutablePresenceProbe.Of(string.Empty).Should().Be(ExecutablePresence.Missing);
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
}
