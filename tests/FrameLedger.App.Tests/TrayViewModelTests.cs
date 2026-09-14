using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.Tests.Update;
using FrameLedger.App.Update;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Settings;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>
/// FR-14's four states from the pipe's events, FR-3.9's pause as one request and the Agent's answer, FR-3.8's
/// balloon only while the window is off screen (one event → one channel), and a safety event never a balloon.
/// </summary>
public sealed class TrayViewModelTests
{
    private sealed class FakeShell : IShellPresence
    {
        public bool IsShown { get; set; } = true;

        public int Reveals { get; private set; }

        public int Exits { get; private set; }

        public void Reveal() => Reveals++;

        public void Quit() => Exits++;
    }

    private sealed class FakeSummaries : ISessionSummaryOpener
    {
        public List<long> Opened { get; } = [];

        public void Open(long sessionId) => Opened.Add(sessionId);
    }

    private sealed class NoNavigation : IPageNavigator
    {
        public List<string> Pages { get; } = [];

        public void Navigate<TPage>()
            where TPage : class => Pages.Add(typeof(TPage).Name);

        public void GoBack()
        {
        }
    }

    private static SessionStartedEvent Started(Guid guid, int tier, string name = "Title") => new(guid, 7, name, 1, tier, DateTimeOffset.UtcNow);

    private static SessionCompletedEvent Completed(Guid guid, long? sessionId) => new(guid, sessionId, "normal", 1, "ok", "exit");

    /// <summary>An updater over a client that finds <paramref name="next"/>, wired to the same link as the tray (P4 PR-5).</summary>
    private static UpdateService Updates(IAgentLink link, UpdateCandidate? next = null) =>
        new(new FakeUpdateClient { Next = next }, link, new RegisteredSettings(new MemorySettings()), new FakeUpdatePrompts(), new FakeShell(), new RecordingStrip());

    [Fact]
    public async Task AnUpdateFoundWhileTheWindowIsOffScreenIsABalloonAndOnScreenIsNot()
    {
        // 08_UI §Notifications policy: "update available" reaches a minimized app as a balloon; on screen the shell's banner is the one channel.
        var link = new FakeAgentLink();
        var candidate = new UpdateCandidate("0.2.0", null, 1);
        using UpdateService hidden = Updates(link, candidate);
        using var vm = new TrayViewModel(link, new FakeShell { IsShown = false }, new FakeSummaries(), new NoNavigation(), hidden);
        var toasts = new List<TrayToastEventArgs>();
        vm.ToastRequested += (_, t) => toasts.Add(t);

        await hidden.CheckSilentlyAsync(TestContext.Current.CancellationToken);

        toasts.Should().ContainSingle().Which.Title.Should().Be(Strings.Update_Toast_Title);
        toasts[0].SessionId.Should().BeNull("a click reveals the window; there is no session to open");

        using UpdateService shown = Updates(link, candidate);
        using var onScreen = new TrayViewModel(link, new FakeShell { IsShown = true }, new FakeSummaries(), new NoNavigation(), shown);
        var none = new List<TrayToastEventArgs>();
        onScreen.ToastRequested += (_, t) => none.Add(t);
        await shown.CheckSilentlyAsync(TestContext.Current.CancellationToken);
        none.Should().BeEmpty();
    }

    [Fact]
    public void TheStateFollowsTheSessionsAndTheirTier()
    {
        var link = new FakeAgentLink();
        using UpdateService updates = Updates(link);
        using var vm = new TrayViewModel(link, new FakeShell(), new FakeSummaries(), new NoNavigation(), updates);
        vm.State.Should().Be(TrayState.Idle);
        vm.Tooltip.Should().Be(Strings.Tray_State_Idle);

        var hooked = Guid.NewGuid();
        var unhooked = Guid.NewGuid();
        link.Raise(IpcMessageType.SessionStarted, Started(unhooked, 2, "Two"));
        vm.State.Should().Be(TrayState.RecordingOnly, "◐: a session runs and nothing is measured");
        vm.Tooltip.Should().Contain("Two");

        link.Raise(IpcMessageType.SessionStarted, Started(hooked, 1, "One"));
        vm.State.Should().Be(TrayState.Capturing, "●: one hooked session is enough");

        link.Raise(IpcMessageType.SessionCompleted, Completed(hooked, 10));
        vm.State.Should().Be(TrayState.RecordingOnly);
        link.Raise(IpcMessageType.SessionCompleted, Completed(unhooked, null));
        vm.State.Should().Be(TrayState.Idle);
    }

    [Fact]
    public async Task PauseIsOneRequestAndTheStateIsTheAgentsAnswer()
    {
        var link = new FakeAgentLink { Answer = static (type, _) => FakeAgentLink.Envelope(IpcMessageType.PauseAck, new PauseAck(string.Equals(type, IpcMessageType.PauseCapture, StringComparison.Ordinal))) };
        using UpdateService updates = Updates(link);
        using var vm = new TrayViewModel(link, new FakeShell(), new FakeSummaries(), new NoNavigation(), updates);
        link.Raise(IpcMessageType.SessionStarted, Started(Guid.NewGuid(), 1));

        await vm.TogglePauseCommand.ExecuteAsync(null);

        link.Sent.Select(static s => s.Type).Should().Equal(IpcMessageType.PauseCapture);
        vm.IsPaused.Should().BeTrue();
        vm.State.Should().Be(TrayState.Paused, "⏸ wins over a running session");
        vm.PauseText.Should().Be(Strings.Tray_Resume);

        await vm.TogglePauseCommand.ExecuteAsync(null);

        link.Sent.Select(static s => s.Type).Should().Equal(IpcMessageType.PauseCapture, IpcMessageType.ResumeCapture);
        vm.IsPaused.Should().BeFalse();
        vm.State.Should().Be(TrayState.Capturing);
    }

    [Fact]
    public async Task WithoutAnAgentPauseSendsNothing()
    {
        var link = new FakeAgentLink { IsConnected = false };
        using UpdateService updates = Updates(link);
        using var vm = new TrayViewModel(link, new FakeShell(), new FakeSummaries(), new NoNavigation(), updates);

        await vm.TogglePauseCommand.ExecuteAsync(null);

        link.Sent.Should().BeEmpty();
        vm.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void TheBalloonIsRaisedOnlyWhileTheWindowIsOffScreenAndItsClickOpensTheSession()
    {
        var link = new FakeAgentLink();
        var shell = new FakeShell { IsShown = true };
        var summaries = new FakeSummaries();
        using UpdateService updates = Updates(link);
        using var vm = new TrayViewModel(link, shell, summaries, new NoNavigation(), updates);
        var toasts = new List<TrayToastEventArgs>();
        vm.ToastRequested += (_, t) => toasts.Add(t);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        link.Raise(IpcMessageType.SessionStarted, Started(first, 1, "Shown"));
        link.Raise(IpcMessageType.SessionCompleted, Completed(first, 41));
        toasts.Should().BeEmpty("the Dashboard's snackbar carries it while the window is on screen");

        shell.IsShown = false;
        link.Raise(IpcMessageType.SessionStarted, Started(second, 1, "Hidden"));
        link.Raise(IpcMessageType.SessionCompleted, Completed(second, 42));
        TrayToastEventArgs toast = toasts.Should().ContainSingle().Subject;
        toast.Title.Should().Be(Strings.Tray_SessionSaved_Title);
        toast.Body.Should().Contain("Hidden", "the game name from SessionStarted");
        toast.SessionId.Should().Be(42);

        vm.OpenLastSavedCommand.Execute(null);
        summaries.Opened.Should().Equal(42);
        shell.Reveals.Should().Be(1);
    }

    [Fact]
    public void ADiscardedSessionAndASafetyEventRaiseNoBalloon()
    {
        var link = new FakeAgentLink();
        using UpdateService updates = Updates(link);
        using var vm = new TrayViewModel(link, new FakeShell { IsShown = false }, new FakeSummaries(), new NoNavigation(), updates);
        var toasts = new List<TrayToastEventArgs>();
        vm.ToastRequested += (_, t) => toasts.Add(t);
        var guid = Guid.NewGuid();

        link.Raise(IpcMessageType.SessionStarted, Started(guid, 1));
        link.Raise(IpcMessageType.CaptureRefused, new CaptureRefusedEvent(7, "Title", "GuardBlocked", "Easy Anti-Cheat", null));
        link.Raise(IpcMessageType.SafetyUnhook, new SafetyUnhookEvent(guid, "BattlEye", null));
        link.Raise(IpcMessageType.SessionCompleted, Completed(guid, null));

        toasts.Should().BeEmpty("08_UI §Notifications policy: safety events are never toasts, and a discarded session has nothing to view");
        vm.LastSavedSessionId.Should().BeNull();
    }

    [Fact]
    public void TheMenuCommandsReachTheShell()
    {
        var shell = new FakeShell();
        var nav = new NoNavigation();
        using UpdateService updates = Updates(new FakeAgentLink());
        using var vm = new TrayViewModel(new FakeAgentLink(), shell, new FakeSummaries(), nav, updates);

        vm.OpenCommand.Execute(null);
        vm.AgentStatusCommand.Execute(null);
        vm.ExitCommand.Execute(null);

        shell.Reveals.Should().Be(2);
        nav.Pages.Should().Equal("DashboardPage");
        shell.Exits.Should().Be(1);
    }

    [Fact]
    public void ADisconnectClearsTheRunningSessions()
    {
        var link = new FakeAgentLink();
        using UpdateService updates = Updates(link);
        using var vm = new TrayViewModel(link, new FakeShell(), new FakeSummaries(), new NoNavigation(), updates);
        link.Raise(IpcMessageType.SessionStarted, Started(Guid.NewGuid(), 1));
        vm.State.Should().Be(TrayState.Capturing);

        link.IsConnected = false;
        link.State = AgentConnectionState.Offline;
        link.RaiseChanged();

        vm.State.Should().Be(TrayState.Idle, "a session the Agent can no longer report is not shown as running");
    }
}
