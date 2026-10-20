// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>The Dashboard over a scratch ledger and a fake link: totals and recent from the tables; the live cards — one per running session since beta.28 — driven by the session events; a completed session reloads.</summary>
[Collection(StringsCultureCollection.Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the expected texts are resources that follow the UI culture")]
public sealed class DashboardViewModelTests
{
    private sealed class FakeLink : IAgentLink
    {
        public AgentConnectionState State { get; set; } = AgentConnectionState.Connected;

        public StatusAck? Status { get; set; }

        public HelloAck? Hello { get; set; }

        public bool IsConnected => true;

        public event EventHandler? Changed;

        public event EventHandler<AgentEventArgs>? EventReceived;

        public Task<IpcEnvelope> RequestAsync<TRequest>(string type, TRequest payload, CancellationToken ct = default)
            where TRequest : class => throw new NotSupportedException();

        public void Raise<T>(string type, T payload)
            where T : class => EventReceived?.Invoke(this, new AgentEventArgs(IpcCodec.Decode(IpcCodec.Encode(type, null, payload))));

        public void Change() => Changed?.Invoke(this, EventArgs.Empty);

        public void SetLaunchHold(bool hold)
        {
        }
    }

    private sealed class NoSummaries : ISessionSummaryOpener
    {
        public List<long> Opened { get; } = [];

        public void Open(long sessionId) => Opened.Add(sessionId);
    }

    private sealed class FakeNavigator : IPageNavigator
    {
        public List<string> Pages { get; } = [];

        public void Navigate<TPage>()
            where TPage : class => Pages.Add(typeof(TPage).Name);

        public void GoBack() => Pages.Add("back");
    }

    [Fact]
    public async Task TotalsAndRecentComeFromTheLedgerAndAGameOpensFromARecentRow()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddHours(-2), seconds: 600);
        var link = new FakeLink();
        var selection = new GameSelection();
        var nav = new FakeNavigator();

        var summaries = new NoSummaries();
        var vm = new DashboardViewModel(link, s.Library, selection, nav, summaries);
        vm.Attach();
        Task pending1 = vm.Pending;
        await pending1;

        vm.RecentEmpty.Should().BeFalse();
        vm.Recent.Should().ContainSingle().Which.GameName.Should().Be("Alpha");
        vm.GamesTrackedText.Should().Be("1");
        vm.ThisWeekText.Should().Be("1");
        vm.AgentVersion.Should().Be(Strings.Common_NotAvailable, "no HelloAck yet");

        link.Hello = new HelloAck("1.2.3", IpcProtocol.Version, 1, true, "b", false, "l1+lhm", false);
        link.Change();
        vm.AgentVersion.Should().Be("1.2.3");
        vm.Elevated.Should().Be(Strings.Common_Yes);

        vm.OpenGameCommand.Execute(vm.Recent[0]);
        selection.GameId.Should().Be(game.Id);
        nav.Pages.Should().Equal("GameDetailPage");
        vm.OpenSessionCommand.Execute(vm.Recent[0]);
        summaries.Opened.Should().ContainSingle().Which.Should().Be(vm.Recent[0].Id, "a recent row opens its summary (08_UI §Dashboard)");
    }

    [Fact]
    public async Task TheLiveCardFollowsStartedProgressCompletedAndACompletionReloads()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        var link = new FakeLink();
        var vm = new DashboardViewModel(link, s.Library, new GameSelection(), new FakeNavigator(), new NoSummaries());
        vm.Attach();
        Task pending2 = vm.Pending;
        await pending2;
        vm.LiveCards.Should().BeEmpty();
        vm.RecentEmpty.Should().BeTrue();
        var guid = Guid.NewGuid();
        link.Raise(IpcMessageType.SessionStarted, new SessionStartedEvent(guid, game.Id, "Alpha", 4242, 1, DateTimeOffset.UtcNow));
        LiveCaptureViewModel card = vm.LiveCards.Should().ContainSingle().Subject;
        vm.LiveCountText.Should().BeNull("one game needs no count");
        card.IsActive.Should().BeTrue();
        card.WaitingForFrames.Should().BeTrue();
        card.GameName.Should().Be("Alpha");
        card.TierText.Should().Be(Strings.Tier_Hooked);

        link.Raise(IpcMessageType.SessionProgress, new SessionProgressEvent
        {
            SessionGuid = guid,
            ElapsedS = 65,
            Presents5s = 300,
            PresentedFps5s = 60,
            PresentedQualifier = "no_fg_runtime",
            FgMode = "na",
            Upscaler = "dlss",
            UpscalerQuality = "Quality",
            RenderW = 1485,
            RenderH = 835,
            OutputW = 2560,
            OutputH = 1440,
            RtActive = true,
            GpuTempC = 70.6,
            GameVramDedicatedMb = 4100,
            GameRamPrivateMb = 2048,
        });
        card.WaitingForFrames.Should().BeFalse();
        card.Readout.Kind.Should().Be(FpsReadoutKind.Presented);
        card.UpscalerText.Should().Be("DLSS Quality");
        card.RtActive.Should().BeTrue();
        card.GpuTempText.Should().Contain("71");
        card.MemoryText.Should().Be(GameMemoryText.Live(4100, 2048), "the game's own memory from the newest tick (beta.12)");

        // A progress for another session is that session's card (beta.28) and leaves this one alone; the completion of ours
        // ends our card and reloads the lists.
        var other = Guid.NewGuid();
        link.Raise(IpcMessageType.SessionProgress, new SessionProgressEvent { SessionGuid = other, ElapsedS = 1, Presents5s = 10, PresentedQualifier = "census_not_run", FgMode = "na", RtActive = false });
        card.RtActive.Should().BeTrue();
        vm.LiveCards.Select(static c => c.SessionGuid).Should().Equal(guid, other);

        long id = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddMinutes(-2), seconds: 65);
        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(guid, id, "normal", 1, "saved", "TargetExited"));
        Task pending3 = vm.Pending;
        await pending3;
        card.IsActive.Should().BeFalse();
        vm.LiveCards.Should().ContainSingle().Which.SessionGuid.Should().Be(other);
        vm.Recent.Should().ContainSingle();
    }

    /// <summary>
    /// A session already running is on the card when the Dashboard is built (2026-09-23) — it is rebuilt on every visit —
    /// and a held one says why; its completion names its own game.
    /// </summary>
    [Fact]
    public async Task ARunningHeldSessionIsOnTheCardAtOnceAndItsCompletionNamesIt()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        var guid = Guid.NewGuid();
        var link = new FakeLink
        {
            Status = new StatusAck("recording", null, null, [new ActiveSession(guid, game.Id, "Alpha", 42, 2, DateTimeOffset.UtcNow, new SessionHold("RefusedHookNotEnabled"))]),
        };
        using var sessions = new LiveSessions(link);
        var strip = new RecordingStrip();
        using var notices = new SessionNotices(link, strip);
        var vm = new DashboardViewModel(link, s.Library, new GameSelection(), new FakeNavigator(), new NoSummaries(), sessions: sessions);
        vm.Attach();
        Task loaded = vm.Pending;
        await loaded;

        LiveCaptureViewModel card = vm.LiveCards.Should().ContainSingle("until 2026-09-23 this read \"Nothing is being captured.\" for as long as the game ran").Subject;
        card.IsActive.Should().BeTrue();
        card.IsHeld.Should().BeTrue();
        card.GameName.Should().Be("Alpha");
        vm.RunningSessions.Should().Be("1");

        link.Raise(IpcMessageType.SessionHeld, new SessionHeldEvent(guid, 30));
        card.ElapsedText.Should().NotBeEmpty();

        long id = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddMinutes(-1), seconds: 30);
        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(guid, id, "normal", 2, "saved", "RefusedHookNotEnabled", game.Id, "Alpha"));
        Task reloaded = vm.Pending;
        await reloaded;

        vm.LiveCards.Should().BeEmpty();
        vm.LiveIdle.Should().BeTrue();
        strip.Shown.Should().ContainSingle().Which.Body.Should().Contain("Alpha");
        vm.RunningSessions.Should().Be("0");
    }

    [Fact]
    public async Task TheNoticeNamesTheCompletedSessionAndAFaultIsNotANotice()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var link = new FakeLink();
        using var sessions = new LiveSessions(link);
        var strip = new RecordingStrip();
        using var notices = new SessionNotices(link, strip);
        var vm = new DashboardViewModel(link, s.Library, new GameSelection(), new FakeNavigator(), new NoSummaries(), sessions: sessions);
        vm.Attach();
        Task loaded = vm.Pending;
        await loaded;
        var hooked = Guid.NewGuid();
        var held = Guid.NewGuid();
        link.Raise(IpcMessageType.SessionStarted, new SessionStartedEvent(hooked, 1, "Hooked", 1, 1, DateTimeOffset.UtcNow));
        link.Raise(IpcMessageType.SessionStarted, new SessionStartedEvent(held, 2, "Held", 2, 2, DateTimeOffset.UtcNow, new SessionHold("RefusedHookNotEnabled")));
        vm.LiveCards.Select(static c => c.GameName).Should().Equal(["Hooked", "Held"], "each game its card since beta.28, a hooked one first");

        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(held, null, "normal", 2, "discarded", "RefusedHookNotEnabled", 2, "Held"));
        Task first = vm.Pending;
        await first;
        strip.Shown.Should().ContainSingle().Which.Body.Should().Contain("Held", "the completed session's own name, not a card's");
        vm.LiveCards.Should().ContainSingle().Which.GameName.Should().Be("Hooked");

        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(hooked, null, "interrupted", 1, "faulted", "SessionFaulted", 1, "Hooked"));
        Task second = vm.Pending;
        await second;
        strip.Shown.Should().ContainSingle("a faulted session is the strip's CaptureError already; \"discarded\" would say what did not happen");
        vm.LiveCards.Should().BeEmpty();
    }

    /// <summary>
    /// beta.28 (the owner, 2026-10-20: the Dashboard when two or more games run at once): each running session has its card,
    /// hooked ones first and each kind in the order it started; each event reaches its own session's card; the header counts
    /// the games from two on, and the cards stand two to a row.
    /// </summary>
    [Fact]
    public async Task TwoGamesRecordingAtOnceEachHaveTheirOwnCardAndTheirOwnEvents()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        (FakeLink link, LiveSessions sessions, Guid held, Guid hooked) = TwoRecording();
        using LiveSessions owned = sessions;
        DashboardViewModel vm = await AttachedAsync(s, link, sessions);

        vm.LiveCards.Select(static c => c.GameName).Should().Equal(["Hooked second", "Held first"], "a hooked session first");
        vm.LiveCountText.Should().Be(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Dashboard_Live_Count_Format, 2));
        vm.LiveColumns.Should().Be(2);
        vm.LiveIdle.Should().BeFalse();

        link.Raise(IpcMessageType.SessionProgress, new SessionProgressEvent { SessionGuid = hooked, ElapsedS = 60, Presents5s = 600, PresentedFps5s = 120, PresentedQualifier = "no_fg_runtime", FgMode = "na", RtActive = true });
        link.Raise(IpcMessageType.SessionHeld, new SessionHeldEvent(held, 300, GpuTempC: 66));
        LiveCaptureViewModel hookedCard = vm.LiveCards[0];
        LiveCaptureViewModel heldCard = vm.LiveCards[1];
        hookedCard.RtActive.Should().BeTrue();
        hookedCard.Readout.Kind.Should().Be(FpsReadoutKind.Presented);
        hookedCard.ElapsedText.Should().Contain(Formats.Duration(60));
        heldCard.IsHeld.Should().BeTrue("a tick of the other session does not reach this card");
        heldCard.ElapsedText.Should().Contain(Formats.Duration(300));
        heldCard.RtActive.Should().BeFalse();
    }

    [Fact]
    public async Task AThirdGameHasACardAndEachEndLeavesTheOthersInTheOrderTheyStarted()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        (FakeLink link, LiveSessions sessions, Guid held, Guid hooked) = TwoRecording();
        using LiveSessions owned = sessions;
        DashboardViewModel vm = await AttachedAsync(s, link, sessions);
        LiveCaptureViewModel heldCard = vm.LiveCards[1];

        var third = Guid.NewGuid();
        link.Raise(IpcMessageType.SessionStarted, new SessionStartedEvent(third, 3, "Third", 43, 2, DateTimeOffset.UtcNow, new SessionHold("RefusedHookNotEnabled")));
        vm.LiveCards.Select(static c => c.GameName).Should().Equal(["Hooked second", "Held first", "Third"]);
        vm.LiveCountText.Should().Be(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Dashboard_Live_Count_Format, 3));

        await CompleteAsync(vm, link, hooked, 1, "Hooked second");
        vm.LiveCards.Select(static c => c.GameName).Should().Equal(["Held first", "Third"]);
        vm.LiveCards[0].Should().BeSameAs(heldCard, "a card is moved, never rebuilt");

        await CompleteAsync(vm, link, held, 2, "Held first");
        vm.LiveCountText.Should().BeNull("one game left");
        vm.LiveColumns.Should().Be(1);

        await CompleteAsync(vm, link, third, 2, "Third");
        vm.LiveCards.Should().BeEmpty();
        vm.LiveIdle.Should().BeTrue();
    }

    /// <summary>A held game that started first and a hooked one that started after it, as the Agent's status lists them.</summary>
    private static (FakeLink Link, LiveSessions Sessions, Guid Held, Guid Hooked) TwoRecording()
    {
        var held = Guid.NewGuid();
        var hooked = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var link = new FakeLink
        {
            Status = new StatusAck("recording", null, null,
            [
                new ActiveSession(held, 1, "Held first", 41, 2, now.AddMinutes(-5), new SessionHold("RefusedHookNotEnabled")),
                new ActiveSession(hooked, 2, "Hooked second", 42, 1, now.AddMinutes(-1), null),
            ]),
        };
        return (link, new LiveSessions(link), held, hooked);
    }

    private static async Task<DashboardViewModel> AttachedAsync(ScratchLedger s, FakeLink link, LiveSessions sessions)
    {
        var vm = new DashboardViewModel(link, s.Library, new GameSelection(), new FakeNavigator(), new NoSummaries(), sessions: sessions);
        vm.Attach();
        Task loaded = vm.Pending;
        await loaded.ConfigureAwait(true);
        return vm;
    }

    private static async Task CompleteAsync(DashboardViewModel vm, FakeLink link, Guid session, int tier, string name)
    {
        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(session, null, "normal", tier, "discarded", "TargetExited", 0, name));
        Task reloaded = vm.Pending;
        await reloaded.ConfigureAwait(true);
    }
    /// <summary>
    /// beta.23: a Dashboard hears the Agent only while its page is on screen. Every visit made a new one and none went
    /// away — the root container kept them all, each subscribed — so each session's end reloaded every Dashboard ever
    /// opened, on the UI thread, and raised one notice apiece.
    /// </summary>
    [Fact]
    public async Task ADashboardOffScreenHearsNothingAndASessionHasOneNoticeHoweverManyWereOpened()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        GameRow game = await s.GameAsync("Alpha");
        var link = new FakeLink();
        var strip = new RecordingStrip();
        using var notices = new SessionNotices(link, strip);
        var visits = new List<DashboardViewModel>();
        for (int i = 0; i < 5; i++)
        {
            // What navigating does: the page on screen is attached, the one it replaced detached.
            visits.LastOrDefault()?.Detach();
            var vm = new DashboardViewModel(link, s.Library, new GameSelection(), new FakeNavigator(), new NoSummaries());
            vm.Attach();
            visits.Add(vm);
            Task pending = vm.Pending;
            await pending;
        }

        var guid = Guid.NewGuid();
        link.Raise(IpcMessageType.SessionStarted, new SessionStartedEvent(guid, game.Id, "Alpha", 4242, 1, DateTimeOffset.UtcNow));
        visits[^1].LiveCards.Should().ContainSingle("the Dashboard on screen follows the session");
        visits.Take(4).Should().OnlyContain(static v => v.LiveCards.Count == 0, "a Dashboard that was navigated away from hears nothing");

        long id = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddMinutes(-2), seconds: 65);
        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(guid, id, "normal", 1, "saved", "TargetExited"));
        Task reloaded = visits[^1].Pending;
        await reloaded;

        strip.Shown.Should().ContainSingle("one session, one notice — it was one per Dashboard ever opened")
            .Which.Body.Should().Contain("Alpha", "the name the start gave, for a completion that carries none");
        visits[^1].Recent.Should().ContainSingle();
        visits.Take(4).Should().OnlyContain(static v => v.Recent.Count == 0, "only the Dashboard on screen reloads its lists");
    }

    /// <summary>FR-3.8 (beta.23): the notice is the window's while it is shown; a hidden window leaves it to the tray's balloon.</summary>
    [Fact]
    public void ANoticeWaitsForTheWindowAndATrayBalloonCarriesItWhenHidden()
    {
        var link = new FakeLink();
        var strip = new RecordingStrip();
        var presence = new Presence { IsShown = false };
        using var notices = new SessionNotices(link, strip, presence);

        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(Guid.NewGuid(), 7, "normal", 1, "saved", "TargetExited", 1, "Alpha"));
        strip.Shown.Should().BeEmpty("the window is hidden: TrayViewModel's balloon says it");

        presence.IsShown = true;
        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(Guid.NewGuid(), 8, "normal", 1, "saved", "TargetExited", 1, "Alpha"));
        strip.Shown.Should().ContainSingle().Which.Kind.Should().Be("success");
    }

    private sealed class Presence : IShellPresence
    {
        public bool IsShown { get; set; }

        public void Reveal()
        {
        }

        public void Quit()
        {
        }
    }
}
