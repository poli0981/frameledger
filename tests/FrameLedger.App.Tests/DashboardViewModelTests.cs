using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>The Dashboard over a scratch ledger and a fake link: totals and recent from the tables; the live card driven by the three session events; a completed session reloads.</summary>
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
        using var vm = new DashboardViewModel(link, s.Library, selection, nav, summaries, new NoStrip());
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
        using var vm = new DashboardViewModel(link, s.Library, new GameSelection(), new FakeNavigator(), new NoSummaries(), new NoStrip());
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
            VramProcMb = 4100,
        });
        vm.Live.WaitingForFrames.Should().BeFalse();
        vm.Live.Readout.Kind.Should().Be(FpsReadoutKind.Presented);
        vm.Live.UpscalerText.Should().Be("DLSS Quality");
        vm.Live.RtActive.Should().BeTrue();
        vm.Live.GpuTempText.Should().Contain("71");
        vm.Live.VramText.Should().Contain("4100");

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
        using var vm = new DashboardViewModel(link, s.Library, new GameSelection(), new FakeNavigator(), new NoSummaries(), strip, sessions: sessions);
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
        using var vm = new DashboardViewModel(link, s.Library, new GameSelection(), new FakeNavigator(), new NoSummaries(), strip, sessions: sessions);
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
}
