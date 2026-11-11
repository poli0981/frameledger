// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>
/// The Advanced page's view model (owner decisions D101, D105, D107): the Agent is asked only while the page is on
/// screen, a session recorded with the option on is read from the ledger, and a viewer has sessions and no PC.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture")]
public sealed class AdvancedViewModelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheAgentIsAskedOnlyBetweenAttachAndDetach()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        int asked = 0;
        var agent = new FakeAgentLink
        {
            Answer = (_, _) =>
            {
                Interlocked.Increment(ref asked);
                return FakeAgentLink.Envelope(IpcMessageType.AdvancedMachineAck, new AdvancedMachineAck(Ready: false, 0, null, null, [], [], "Unknown", null, "Unknown", "Unknown"));
            },
        };
        using var feed = new AdvancedMachineFeed(agent, TimeProvider.System, TimeSpan.FromMilliseconds(20));
        AdvancedViewModel vm = Make(s, feed);
        await vm.Pending;

        await Task.Delay(80, Ct);
        Volatile.Read(ref asked).Should().Be(0, "D107: a page that is not on screen asks nothing, so the Agent reads no sensor for it");

        vm.Attach();
        vm.Attach();
        await UntilAsync(() => Volatile.Read(ref asked) >= 2);
        vm.Detach();
        await Task.Delay(100, Ct);
        int after = Volatile.Read(ref asked);
        await Task.Delay(150, Ct);

        Volatile.Read(ref asked).Should().Be(after, "the page left the screen: the asking stops, and the Agent's lease runs out");
        feed.Current.State.Should().Be(AdvancedFeedState.Stopped);
    }

    [Fact]
    public async Task WhatTheAgentReadIsShownWithItsStateItsFactsItsRowsAndAChartOfTheFirstEntry()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        using var feed = new AdvancedMachineFeed(new FakeAgentLink(), TimeProvider.System);
        AdvancedViewModel vm = Make(s, feed);
        await vm.Pending;
        int presented = 0;
        vm.LivePresented += (_, _) => presented++;

        vm.ShowMachine(AdvancedFixtures.View());

        vm.MachineStatus.Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.Advanced_State_Live,
            AdvancedFixtures.T0.AddSeconds(1).ToLocalTime().ToString("T", CultureInfo.CurrentCulture)));
        vm.MachineGroups.Should().HaveCount(3);
        vm.LiveRows.Should().HaveCount(3);
        vm.LiveMetrics.Select(static m => m.Id).Should().Equal("nvapi.vram.in_use", "lhm.gpu.fan");
        vm.SelectedLiveMetric!.Id.Should().Be("nvapi.vram.in_use", "the chart draws something without being asked");
        vm.ShowElevationHint.Should().BeTrue("the agent is not an administrator's: the page says what that leaves unread and where to change it");
        vm.SourcesNow.Should().Be(AdvancedPresenter.SourcesLine(AdvancedFixtures.Sources));
        presented.Should().BeGreaterThan(0);

        // Another state keeps what was shown and says why it is not new.
        vm.ShowMachine(AdvancedFixtures.View(AdvancedFeedState.NoAgent));
        vm.MachineStatus.Should().Be(Strings.Advanced_State_NoAgent);
        vm.LiveRows.Should().HaveCount(3);
        vm.SourcesNow.Should().BeNull("with no answer the sources' state is not known, and is not claimed");

        vm.ShowMachine(AdvancedMachineView.Stopped with { State = AdvancedFeedState.Off });
        vm.MachineStatus.Should().Be(Strings.Advanced_State_Off);
        vm.LiveRows.Should().BeEmpty();
        vm.SelectedLiveMetric.Should().BeNull();
        vm.LiveChartEmpty.Should().BeTrue();

        vm.ShowMachine(AdvancedMachineView.Stopped with { State = AdvancedFeedState.OlderAgent });
        vm.MachineStatus.Should().Be(Strings.Advanced_State_OlderAgent);
        vm.ShowMachine(AdvancedMachineView.Stopped with { State = AdvancedFeedState.Waiting });
        vm.MachineStatus.Should().Be(Strings.Advanced_State_Waiting);
    }

    [Fact]
    public async Task WithNoSessionRecordedWithTheOptionOnTheSessionTabSaysSo()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        await s.SessionAsync(game.Id, AdvancedFixtures.T0);
        using var feed = new AdvancedMachineFeed(new FakeAgentLink(), TimeProvider.System);

        AdvancedViewModel vm = Make(s, feed);
        await vm.Pending;

        vm.NoSessions.Should().BeTrue("a session recorded while the option was off has no row, and is not offered");
        vm.SessionOptions.Should().BeEmpty();
        vm.SelectedSession.Should().BeNull();
        vm.SessionChartEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task TheNewestAdvancedSessionIsShownWithItsFactsItsStatisticsAndItsChart()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        long older = await s.SessionAsync(game.Id, AdvancedFixtures.T0.AddHours(-2), sensors: AdvancedFixtures.Blobs(), advanced: AdvancedFixtures.Row());
        long newer = await s.SessionAsync(game.Id, AdvancedFixtures.T0, sensors: AdvancedFixtures.Blobs(), advanced: AdvancedFixtures.Row());
        await s.SessionAsync(game.Id, AdvancedFixtures.T0.AddHours(1));
        using var feed = new AdvancedMachineFeed(new FakeAgentLink(), TimeProvider.System);

        AdvancedViewModel vm = Make(s, feed);
        await vm.Pending;

        vm.NoSessions.Should().BeFalse();
        vm.SessionOptions.Select(static o => o.SessionId).Should().Equal(newer, older);
        vm.SessionOptions[0].Text.Should().Be("Alpha — " + Formats.Date(AdvancedFixtures.T0));
        vm.SelectedSession!.SessionId.Should().Be(newer);
        vm.SessionSources.Should().Be(AdvancedPresenter.SourcesLine(AdvancedFixtures.Sources));
        vm.SessionGroups.SelectMany(static g => g.Rows).Should().HaveCount(AdvancedFixtures.MachineFacts.Count + AdvancedFixtures.SessionFacts.Count,
            "the PC's facts as they were then, and the session's own");
        vm.SessionStats.Should().HaveCount(3);
        vm.SessionHasNoStats.Should().BeFalse();
        vm.SessionSeriesGone.Should().BeFalse();
        vm.SessionMetrics.Select(static m => m.Id).Should().Equal("nvapi.vram.in_use", "lhm.gpu.fan", "lhm.board.voltage");
        vm.SessionLines.Should().ContainSingle().Which.Ys.Should().Equal(5000, 5100, 5200, 5300, 5400, 5500);
        vm.SessionUnit.Should().Be("MiB");

        int presented = 0;
        vm.SessionPresented += (_, _) => presented++;
        vm.SelectedSessionMetric = vm.SessionMetrics[1];

        presented.Should().Be(1);
        vm.SessionLines.Should().HaveCount(2, "the two fans");
        vm.SessionUnit.Should().Be("RPM");

        vm.SelectedSession = vm.SessionOptions[1];
        await vm.Pending;
        vm.SelectedSessionMetric!.Id.Should().Be("lhm.gpu.fan", "another session keeps the entry the chart was showing where it has it");
    }

    [Fact]
    public async Task ASessionWhoseRawSeriesWereSweptKeepsItsStatisticsAndSaysTheSeriesAreGone()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        await s.SessionAsync(game.Id, AdvancedFixtures.T0, advanced: AdvancedFixtures.Row());
        using var feed = new AdvancedMachineFeed(new FakeAgentLink(), TimeProvider.System);

        AdvancedViewModel vm = Make(s, feed);
        await vm.Pending;

        vm.SessionStats.Should().HaveCount(3);
        vm.SessionSeriesGone.Should().BeTrue("retention removes the per-second series and keeps the statistics");
        vm.SessionMetrics.Should().BeEmpty();
        vm.SessionChartEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task ASessionAskedForFromItsSummaryIsTheOneShownOnTheSessionTab()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        long older = await s.SessionAsync(game.Id, AdvancedFixtures.T0.AddHours(-2), sensors: AdvancedFixtures.Blobs(), advanced: AdvancedFixtures.Row());
        await s.SessionAsync(game.Id, AdvancedFixtures.T0, sensors: AdvancedFixtures.Blobs(), advanced: AdvancedFixtures.Row());
        using var feed = new AdvancedMachineFeed(new FakeAgentLink { IsConnected = false }, TimeProvider.System);
        var selection = new AdvancedSelection();
        AdvancedViewModel vm = Make(s, feed, selection);
        await vm.Pending;
        vm.SelectedTab.Should().Be(0);

        selection.Request(older);
        vm.Attach();
        await vm.Pending;

        vm.SelectedSession!.SessionId.Should().Be(older);
        vm.SelectedTab.Should().Be(1, "the summary's button leads to that session, not to this PC");
        selection.Take().Should().BeNull("asked for once: opened from the navigation pane afterwards, the page shows its newest session");

        // And while the page is already on screen, a second summary's request is taken at once.
        long newest = vm.SessionOptions[0].SessionId;
        selection.Request(newest);
        await vm.Pending;
        vm.SelectedSession!.SessionId.Should().Be(newest);
        vm.Detach();
    }

    [Fact]
    public async Task AViewerOpensOnItsSessionsAndAsksNoAgent()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        await s.SessionAsync(game.Id, AdvancedFixtures.T0, sensors: AdvancedFixtures.Blobs(), advanced: AdvancedFixtures.Row());
        var agent = new FakeAgentLink { IsConnected = false };
        using var feed = new AdvancedMachineFeed(agent, TimeProvider.System, TimeSpan.FromMilliseconds(20));

        AdvancedViewModel vm = Make(s, feed, mode: new UiMode(true, System.IO.Path.GetTempPath()));
        await vm.Pending;
        vm.Attach();
        await Task.Delay(100, Ct);

        vm.SelectedTab.Should().Be(1, "a copy is opened to look at its sessions");
        vm.MachineStatus.Should().Be(Strings.Advanced_State_Viewer);
        agent.Sent.Should().BeEmpty("a viewer has no Agent, and does not look for one every second");
        vm.SelectedSession.Should().NotBeNull();
        vm.Detach();
    }

    [Fact]
    public async Task TheSummaryOffersAdvancedMetricsOnlyForASessionThatStoredThem()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        long with = await s.SessionAsync(game.Id, AdvancedFixtures.T0, sensors: AdvancedFixtures.Blobs(), advanced: AdvancedFixtures.Row());
        long without = await s.SessionAsync(game.Id, AdvancedFixtures.T0.AddHours(1));
        long? opened = null;

        SessionSummaryViewModel on = Summary(s);
        on.ShowAdvanced = id => opened = id;
        await on.LoadAsync(with, Ct);
        on.HasAdvanced.Should().BeTrue();
        on.OpenAdvancedCommand.Execute(null);
        opened.Should().Be(with);

        SessionSummaryViewModel off = Summary(s);
        await off.LoadAsync(without, Ct);
        off.HasAdvanced.Should().BeFalse("recorded with the option off: there is nothing to lead to");
    }

    [Fact]
    public void CtrlSixOpensThePageOnlyWhileTheOptionIsOn()
    {
        var navigator = new RecordingNavigator();
        bool was = AdvancedOption.IsOn;
        try
        {
            AdvancedOption.IsOn = false;
            ShellShortcuts.Navigate(navigator, "6").Should().BeFalse("D105: the number stays free while the option is off");
            navigator.Pages.Should().BeEmpty();

            int notices = 0;
            void Count(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => notices++;
            AdvancedOption.StaticPropertyChanged += Count;
            AdvancedOption.IsOn = true;
            AdvancedOption.IsOn = true;
            AdvancedOption.StaticPropertyChanged -= Count;

            notices.Should().Be(1, "the navigation item is bound to it, and is told once per change");
            ShellShortcuts.Navigate(navigator, "6").Should().BeTrue();
            navigator.Pages.Should().Equal(nameof(Pages.AdvancedPage));
        }
        finally
        {
            AdvancedOption.IsOn = was;
        }
    }

    private static AdvancedViewModel Make(ScratchLedger s, AdvancedMachineFeed feed, AdvancedSelection? selection = null, UiMode? mode = null) =>
        new(feed, s.Sessions, s.Games, new SessionSeriesLoader(s.Sessions), selection ?? new AdvancedSelection(), mode);

    private static SessionSummaryViewModel Summary(ScratchLedger s) => new(s.Sessions, s.Games, s.Annotations,
        new Infrastructure.Persistence.SqliteHardwareSnapshotRepository(s.Db), new SessionSeriesLoader(s.Sessions), new NoOverride(), new NoSaver(), new NoStrip());

    private static async Task UntilAsync(Func<bool> met)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (!met())
        {
            DateTime.UtcNow.Should().BeBefore(deadline);
            await Task.Delay(10, Ct).ConfigureAwait(false);
        }
    }

    private sealed class RecordingNavigator : IPageNavigator
    {
        public List<string> Pages { get; } = [];

        public void Navigate<TPage>()
            where TPage : class => Pages.Add(typeof(TPage).Name);

        public void GoBack()
        {
        }
    }

    private sealed class NoOverride : ITriStateOverridePrompt
    {
        public Task<TriStateOverrideChoice?> AskAsync(TriStateChipModel chip, CancellationToken ct = default) => Task.FromResult<TriStateOverrideChoice?>(null);
    }

    private sealed class NoSaver : IFileSaver
    {
        public string? PickSavePath(string filter, string suggestedName) => null;
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
}
