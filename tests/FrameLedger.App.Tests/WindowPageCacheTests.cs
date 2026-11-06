// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Abstractions;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.34 (owner request 2026-11-06): the library page is kept by the window that built it and shown again on a later
/// visit; every other page is the container's, new each visit; a new window — a language change — builds its own.
/// And the shell opens a rebuilt window on the page that was on screen, however it was reached.
/// </summary>
public sealed class WindowPageCacheTests
{
    private sealed class NewEachTime : INavigationViewPageProvider
    {
        public int Built { get; private set; }

        public object? GetPage(Type pageType)
        {
            Built++;
            return new object();
        }
    }

    [Fact]
    public void TheLibraryIsKeptByItsWindowAndEveryOtherPageIsNewEachVisit()
    {
        var container = new NewEachTime();
        var window = new WindowPageCache(container);

        object games = window.GetPage(typeof(GamesPage))!;
        window.GetPage(typeof(GamesPage)).Should().BeSameAs(games, "shown again, it is the page already built");
        window.GetPage(typeof(DashboardPage)).Should().NotBeSameAs(window.GetPage(typeof(DashboardPage)), "transient, as before");
        container.Built.Should().Be(3);

        new WindowPageCache(container).GetPage(typeof(GamesPage)).Should().NotBeSameAs(games,
            "a rebuilt window builds its own, in its own language, and never shares one page between two windows");
        WindowPageCache.Kept.Should().BeEquivalentTo([typeof(GamesPage)]);
        FluentActions.Invoking(() => window.GetPage(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ARebuildOpensOnThePageOnScreenHoweverItWasReached()
    {
        var shell = new ShellHost(new ServiceCollection().BuildServiceProvider(), null!, null!, new WindowClosePolicy());
        shell.Page.Should().Be<DashboardPage>();

        shell.Remember(typeof(GameDetailPage));
        shell.NoteShown(live: true, page: new SettingsPageStandIn());
        shell.Page.Should().Be<SettingsPageStandIn>("a page reached by the pane is remembered too");

        shell.NoteShown(live: false, page: new object());
        shell.Page.Should().Be<SettingsPageStandIn>("the closing window of a rebuild changes nothing");
        shell.NoteShown(live: true, page: null);
        shell.Page.Should().Be<SettingsPageStandIn>();
    }

    private sealed class SettingsPageStandIn;
}
