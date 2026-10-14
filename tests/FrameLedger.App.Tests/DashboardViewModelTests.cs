// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>The Dashboard over a scratch ledger and a fake link: totals and recent from the tables; the live card driven by the three session events; a completed session reloads.</summary>
[Collection(StringsCultureCollection.Name)]
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
        vm.Live.IsActive.Should().BeFalse();
        vm.RecentEmpty.Should().BeTrue();

        var guid = Guid.NewGuid();
        link.Raise(IpcMessageType.SessionStarted, new SessionStartedEvent(guid, game.Id, "Alpha", 4242, 1, DateTimeOffset.UtcNow));
        vm.Live.IsActive.Should().BeTrue();
        vm.Live.WaitingForFrames.Should().BeTrue();
        vm.Live.GameName.Should().Be("Alpha");
        vm.Live.TierText.Should().Be(Strings.Tier_Hooked);

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
        vm.Live.WaitingForFrames.Should().BeFalse();
        vm.Live.Readout.Kind.Should().Be(FpsReadoutKind.Presented);
        vm.Live.UpscalerText.Should().Be("DLSS Quality");
        vm.Live.RtActive.Should().BeTrue();
        vm.Live.GpuTempText.Should().Contain("71");
        vm.Live.MemoryText.Should().Be(GameMemoryText.Live(4100, 2048), "the game's own memory from the newest tick (beta.12)");

        // A progress for another session is ignored; the completion of ours ends the card and reloads the lists.
        link.Raise(IpcMessageType.SessionProgress, new SessionProgressEvent { SessionGuid = Guid.NewGuid(), ElapsedS = 1, Presents5s = 10, PresentedQualifier = "census_not_run", FgMode = "na", RtActive = false });
        vm.Live.RtActive.Should().BeTrue();

        long id = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddMinutes(-2), seconds: 65);
        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(guid, id, "normal", 1, "saved", "TargetExited"));
        Task pending3 = vm.Pending;
        await pending3;
        vm.Live.IsActive.Should().BeFalse();
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

        vm.Live.IsActive.Should().BeTrue("until 2026-09-23 this read \"Nothing is being captured.\" for as long as the game ran");
        vm.Live.IsHeld.Should().BeTrue();
        vm.Live.GameName.Should().Be("Alpha");
        vm.RunningSessions.Should().Be("1");

        link.Raise(IpcMessageType.SessionHeld, new SessionHeldEvent(guid, 30));
        vm.Live.ElapsedText.Should().NotBeEmpty();

        long id = await s.SessionAsync(game.Id, DateTimeOffset.UtcNow.AddMinutes(-1), seconds: 30);
        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(guid, id, "normal", 2, "saved", "RefusedHookNotEnabled", game.Id, "Alpha"));
        Task reloaded = vm.Pending;
        await reloaded;

        vm.Live.IsActive.Should().BeFalse();
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
        vm.Live.GameName.Should().Be("Hooked", "a hooked session keeps the card");

        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(held, null, "normal", 2, "discarded", "RefusedHookNotEnabled", 2, "Held"));
        Task first = vm.Pending;
        await first;
        strip.Shown.Should().ContainSingle().Which.Body.Should().Contain("Held", "the completed session's own name, not the card's");
        vm.Live.GameName.Should().Be("Hooked");

        link.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(hooked, null, "interrupted", 1, "faulted", "SessionFaulted", 1, "Hooked"));
        Task second = vm.Pending;
        await second;
        strip.Shown.Should().ContainSingle("a faulted session is the strip's CaptureError already; \"discarded\" would say what did not happen");
        vm.Live.IsActive.Should().BeFalse();
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
        visits[^1].Live.IsActive.Should().BeTrue("the Dashboard on screen follows the session");
        visits.Take(4).Should().OnlyContain(static v => !v.Live.IsActive, "a Dashboard that was navigated away from hears nothing");

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
