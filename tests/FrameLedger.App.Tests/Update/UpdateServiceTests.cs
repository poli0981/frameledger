using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.Update;
using FrameLedger.Application.Settings;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests.Update;

/// <summary>
/// <c>11_UPDATER</c> §Flow over the port: the startup check's two skips and its silence on failure, the manual
/// check's dialogs and the error rows, the download landing Ready or Deferred by whether a session runs (FR-12),
/// and the apply — <c>Shutdown</c> to the Agent, the relaunch held, the updater told, the host ended — or refused
/// with everything left as it was.
/// </summary>
public sealed class UpdateServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FakeShell : IShellPresence
    {
        public bool IsShown { get; set; } = true;

        public int Quits { get; private set; }

        public void Reveal()
        {
        }

        public void Quit() => Quits++;
    }

    private sealed class Harness
    {
        public FakeUpdateClient Client { get; } = new();

        public FakeAgentLink Agent { get; } = new();

        public MemorySettings Store { get; } = new();

        public FakeUpdatePrompts Prompts { get; } = new();

        public FakeShell Shell { get; } = new();

        public RecordingStrip Strip { get; } = new();

        public List<string> Available { get; } = [];

        public UpdateService Service { get; }

        public Harness(TimeSpan? agentStop = null)
        {
            Service = new UpdateService(Client, Agent, new RegisteredSettings(Store), Prompts, Shell, Strip, agentStop);
            Service.Available += (_, e) => Available.Add(e.Version);
        }
    }

    private static UpdateCandidate Candidate(string version = "0.2.0") => new(version, "- notes", 42L * 1024 * 1024);

    private static StatusAck Status(string state, int sessions = 0) =>
        new(state, null, null, [.. Enumerable.Range(0, sessions).Select(static i => new ActiveSession(Guid.NewGuid(), 1, "Title", 100 + i, 1, DateTimeOffset.UtcNow))]);

    [Fact]
    public async Task TheStartupCheckIsSkippedForACopyTheInstallerDidNotLayDown()
    {
        var h = new Harness();
        h.Client.IsInstalled = false;
        h.Client.Next = Candidate();

        await h.Service.CheckSilentlyAsync(Ct);

        h.Client.Checks.Should().BeEmpty("a bin/ build cannot update itself and must not ask the feed");
        h.Service.Stage.Should().Be(UpdateStage.Idle);
    }

    [Fact]
    public async Task TheStartupCheckIsSkippedWhenTheSettingIsOff()
    {
        var h = new Harness();
        h.Store.Rows[SettingsRegistry.UpdateAutoCheck.Key] = "0";
        h.Client.Next = Candidate();

        await h.Service.CheckSilentlyAsync(Ct);

        h.Client.Checks.Should().BeEmpty("11_UPDATER: the startup check runs 'if enabled'");
    }

    [Fact]
    public async Task AStartupCheckThatFindsNothingStaysIdleAndShowsNothing()
    {
        var h = new Harness();

        await h.Service.CheckSilentlyAsync(Ct);

        h.Client.Checks.Should().ContainSingle().Which.Should().BeFalse("the stable channel asks for no pre-releases");
        h.Service.Stage.Should().Be(UpdateStage.Idle);
        h.Prompts.Offered.Should().BeEmpty();
        h.Prompts.UpToDate.Should().BeEmpty("silent means silent");
        h.Available.Should().BeEmpty();
    }

    [Fact]
    public async Task AStartupCheckDownloadsInTheBackgroundAndLandsReadyWhenNoSessionRuns()
    {
        var h = new Harness();
        h.Client.Next = Candidate();

        await h.Service.CheckSilentlyAsync(Ct);

        h.Available.Should().ContainSingle("the tray's toast is raised once, when found").Which.Should().Be("0.2.0");
        h.Client.Downloaded.Should().Equal("0.2.0");
        h.Service.Stage.Should().Be(UpdateStage.Ready);
        h.Service.Version.Should().Be("0.2.0");
        h.Service.Percent.Should().Be(100);
        h.Prompts.Offered.Should().BeEmpty("the startup check never asks; the banner offers the restart");
    }

    [Fact]
    public async Task TheBetaChannelAsksForPrereleases()
    {
        var h = new Harness();
        h.Store.Rows[SettingsRegistry.UpdateChannel.Key] = "beta";

        await h.Service.CheckSilentlyAsync(Ct);

        h.Client.Checks.Should().ContainSingle().Which.Should().BeTrue("beta includes GitHub pre-releases");
    }

    [Fact]
    public async Task ADownloadWhileASessionRunsIsDeferredAndBecomesReadyWhenTheSessionEnds()
    {
        var h = new Harness();
        h.Agent.Status = Status("capturing", 1);
        h.Agent.RaiseChanged();
        h.Client.Next = Candidate();

        await h.Service.CheckSilentlyAsync(Ct);
        h.Service.Stage.Should().Be(UpdateStage.Deferred, "FR-12: never apply — or offer the apply — while a game is hooked");

        h.Agent.Status = Status("idle");
        h.Agent.RaiseChanged();
        h.Service.Stage.Should().Be(UpdateStage.Ready);

        var guid = Guid.NewGuid();
        h.Agent.Raise(IpcMessageType.SessionStarted, new SessionStartedEvent(guid, 1, "Title", 100, 1, DateTimeOffset.UtcNow));
        h.Service.Stage.Should().Be(UpdateStage.Deferred, "a session that started after the download defers it again");
        h.Agent.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(guid, 5, "normal", 1, "saved", "exit"));
        h.Service.Stage.Should().Be(UpdateStage.Ready);
    }

    [Fact]
    public async Task ASessionListedAtConnectEndsWithItsCompletionNotAtTheNextReconnect()
    {
        var h = new Harness();
        StatusAck status = Status("capturing", 1);
        h.Agent.Status = status;
        h.Agent.RaiseChanged();
        h.Client.Next = Candidate();

        await h.Service.CheckSilentlyAsync(Ct);
        h.Service.Stage.Should().Be(UpdateStage.Deferred);

        Guid running = status.ActiveSessions[0].SessionGuid;
        h.Agent.Raise(IpcMessageType.SessionCompleted, new SessionCompletedEvent(running, 5, "normal", 1, "saved", "exit"));

        h.Service.Stage.Should().Be(UpdateStage.Ready, "until 2026-09-23 the connect-time status held this at Deferred until the App reconnected, hours later");
        h.Service.IsSessionActive.Should().BeFalse();

        h.Agent.State = AgentConnectionState.Offline;
        h.Agent.RaiseChanged();
        h.Service.IsSessionActive.Should().BeFalse("a lost Agent lists nothing; its sessions come back with the next connect's status");
    }

    [Fact]
    public async Task AStartupFailureIsALogLineNotADialog()
    {
        var h = new Harness();
        h.Client.CheckThrows = new UpdateException(UpdateFailure.Offline, "dns", null);

        await h.Service.CheckSilentlyAsync(Ct);

        h.Prompts.Failed.Should().BeEmpty("NFR-10: offline on the startup check is Information, no dialog");
        h.Service.Stage.Should().Be(UpdateStage.Idle);
    }

    [Fact]
    public async Task TheManualCheckOnAnUninstalledCopySaysSoAndAsksNothing()
    {
        var h = new Harness();
        h.Client.IsInstalled = false;

        await h.Service.CheckInteractivelyAsync(Ct);

        h.Prompts.NotInstalled.Should().Be(1);
        h.Client.Checks.Should().BeEmpty();
    }

    [Fact]
    public async Task TheManualCheckSaysUpToDateWithTheInstalledVersion()
    {
        var h = new Harness();

        await h.Service.CheckInteractivelyAsync(Ct);

        h.Prompts.UpToDate.Should().Equal("0.1.0");
        h.Service.Stage.Should().Be(UpdateStage.Idle);
    }

    [Fact]
    public async Task TheManualCheckOffersAndADeclineDownloadsNothing()
    {
        var h = new Harness();
        h.Client.Next = Candidate();
        h.Prompts.Accept = false;

        await h.Service.CheckInteractivelyAsync(Ct);

        h.Prompts.Offered.Should().Equal("0.2.0");
        h.Client.Downloaded.Should().BeEmpty();
        h.Service.Stage.Should().Be(UpdateStage.Idle);
    }

    [Fact]
    public async Task TheManualCheckOffersAndAnAcceptDownloadsToReady()
    {
        var h = new Harness();
        h.Client.Next = Candidate();

        await h.Service.CheckInteractivelyAsync(Ct);

        h.Client.Downloaded.Should().Equal("0.2.0");
        h.Service.Stage.Should().Be(UpdateStage.Ready);
    }

    [Theory]
    [InlineData(UpdateFailure.NotFound)]
    [InlineData(UpdateFailure.RateLimited)]
    [InlineData(UpdateFailure.Server)]
    [InlineData(UpdateFailure.Offline)]
    [InlineData(UpdateFailure.Unknown)]
    public async Task TheManualCheckMapsAFailureToItsRow(UpdateFailure failure)
    {
        var h = new Harness();
        h.Client.CheckThrows = new UpdateException(failure, "x", null);

        await h.Service.CheckInteractivelyAsync(Ct);

        h.Prompts.Failed.Should().Equal(failure);
        h.Service.Stage.Should().Be(UpdateStage.Idle);
    }

    [Fact]
    public async Task ACorruptDownloadOnTheManualCheckIsItsRow()
    {
        var h = new Harness();
        h.Client.Next = Candidate();
        h.Client.DownloadThrows.Enqueue(new UpdateException(UpdateFailure.Corrupt, "hash", null));
        h.Client.DownloadThrows.Enqueue(new UpdateException(UpdateFailure.Corrupt, "hash again", null));

        await h.Service.CheckInteractivelyAsync(Ct);

        h.Client.DownloadAttempts.Should().Be(2, "11_UPDATER: a hash mismatch retries once, then it is the dialog's");
        h.Prompts.Failed.Should().Equal(UpdateFailure.Corrupt);
        h.Service.Stage.Should().Be(UpdateStage.Idle);
    }

    [Fact]
    public async Task OneCorruptDownloadIsRetriedAndTheSecondLandsReady()
    {
        var h = new Harness();
        h.Client.Next = Candidate();
        h.Client.DownloadThrows.Enqueue(new UpdateException(UpdateFailure.Corrupt, "hash", null));

        await h.Service.CheckSilentlyAsync(Ct);

        h.Client.DownloadAttempts.Should().Be(2);
        h.Client.Downloaded.Should().Equal("0.2.0");
        h.Service.Stage.Should().Be(UpdateStage.Ready);
    }

    [Fact]
    public async Task ADownloadThatFailsForAnotherReasonIsNotRetried()
    {
        var h = new Harness();
        h.Client.Next = Candidate();
        h.Client.DownloadThrows.Enqueue(new UpdateException(UpdateFailure.Offline, "dropped", null));

        await h.Service.CheckSilentlyAsync(Ct);

        h.Client.DownloadAttempts.Should().Be(1);
        h.Service.Stage.Should().Be(UpdateStage.Idle);
    }

    [Fact]
    public async Task AFaultTheClientDidNotClassifyIsTheUnknownRowNotAnUnhandledCommand()
    {
        var h = new Harness();
        h.Store.Rows[SettingsRegistry.UpdateChannel.Key] = "stable";
        using var faulting = new UpdateService(new ThrowingClient(), h.Agent, new RegisteredSettings(h.Store), h.Prompts, h.Shell, h.Strip);

        await faulting.CheckInteractivelyAsync(Ct);

        h.Prompts.Failed.Should().Equal(UpdateFailure.Unknown);
        faulting.Stage.Should().Be(UpdateStage.Idle);
    }

    /// <summary>A client whose check throws something it did not wrap — the shape a future bug in the adapter would have.</summary>
    private sealed class ThrowingClient : IUpdateClient
    {
        public bool IsInstalled => true;

        public string? CurrentVersion => "0.1.0";

        public Task<UpdateCandidate?> CheckAsync(bool includePrereleases, CancellationToken ct = default) =>
            Task.FromException<UpdateCandidate?>(new InvalidOperationException("not wrapped"));

        public Task DownloadAsync(UpdateCandidate candidate, IProgress<int>? progress, CancellationToken ct = default) => Task.CompletedTask;

        public void ApplyOnExit(UpdateCandidate candidate)
        {
        }
    }

    [Fact]
    public async Task RestartToUpdateRefusesWhileASessionRunsAndLeavesThePackageDeferred()
    {
        var h = new Harness();
        h.Client.Next = Candidate();
        await h.Service.CheckSilentlyAsync(Ct);
        h.Service.Stage.Should().Be(UpdateStage.Ready);
        h.Agent.Status = Status("capturing", 1);
        h.Agent.RaiseChanged();

        await h.Service.RestartToUpdateAsync(Ct);

        h.Service.Stage.Should().Be(UpdateStage.Deferred);
        h.Client.Applied.Should().BeEmpty("FR-12");
        h.Agent.Sent.Should().BeEmpty("the Agent is not even asked");
        h.Shell.Quits.Should().Be(0);
        h.Strip.Shown.Should().ContainSingle().Which.Kind.Should().Be("warn");
    }

    [Fact]
    public async Task RestartToUpdateStopsTheAgentHoldsItsRelaunchAppliesAndEndsTheHost()
    {
        var h = new Harness();
        h.Client.Next = Candidate();
        await h.Service.CheckSilentlyAsync(Ct);
        h.Agent.Answer = (type, _) =>
        {
            type.Should().Be(IpcMessageType.Shutdown);
            h.Agent.IsConnected = false;
            return FakeAgentLink.Envelope(IpcMessageType.ShutdownAck, new ShutdownAck());
        };

        await h.Service.RestartToUpdateAsync(Ct);

        h.Agent.Sent.Should().ContainSingle().Which.Type.Should().Be(IpcMessageType.Shutdown);
        h.Agent.Holds.Should().ContainSingle("the connection must not start the Agent under the updater").Which.Should().BeTrue();
        h.Client.Applied.Should().Equal("0.2.0");
        h.Shell.Quits.Should().Be(1);
        h.Service.Stage.Should().Be(UpdateStage.Applying);
    }

    [Fact]
    public async Task RestartToUpdateWithNoAgentConnectedAppliesWithoutAsking()
    {
        var h = new Harness();
        h.Agent.IsConnected = false;
        h.Client.Next = Candidate();
        await h.Service.CheckSilentlyAsync(Ct);

        await h.Service.RestartToUpdateAsync(Ct);

        h.Agent.Sent.Should().BeEmpty();
        h.Client.Applied.Should().Equal("0.2.0");
        h.Shell.Quits.Should().Be(1);
    }

    [Fact]
    public async Task AnAgentThatDoesNotStopInTimeLeavesEverythingAsItWas()
    {
        var h = new Harness(TimeSpan.FromMilliseconds(300));
        h.Client.Next = Candidate();
        await h.Service.CheckSilentlyAsync(Ct);
        h.Agent.Answer = static (_, _) => FakeAgentLink.Envelope(IpcMessageType.ShutdownAck, new ShutdownAck());

        await h.Service.RestartToUpdateAsync(Ct);

        h.Agent.Holds.Should().Equal([true, false], "held for the attempt, released when it was abandoned");
        h.Client.Applied.Should().BeEmpty("an Agent still holding the install directory would make the apply fail half way");
        h.Shell.Quits.Should().Be(0);
        h.Service.Stage.Should().Be(UpdateStage.Ready, "the package is still there for the next try");
        h.Strip.Shown.Should().ContainSingle().Which.Body.Should().Be(Strings.Update_AgentStillRunning);
    }

    [Fact]
    public async Task RestartToUpdateWithNothingDownloadedDoesNothing()
    {
        var h = new Harness();

        await h.Service.RestartToUpdateAsync(Ct);

        h.Client.Applied.Should().BeEmpty();
        h.Shell.Quits.Should().Be(0);
        h.Agent.Holds.Should().BeEmpty();
    }
}
