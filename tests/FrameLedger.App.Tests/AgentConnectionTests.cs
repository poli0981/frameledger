using System.IO;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Application.Ipc;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Infrastructure.Startup;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>
/// <c>07_IPC</c> §Client behavior as a state machine, against a real <see cref="PipeServer"/> in this process on
/// a test-named pipe: connect rounds, starting the Agent when it is absent, <c>Hello</c> on connect, offline
/// when the server goes, and connected again when it returns. The launcher is a fake that can start the server.
/// </summary>
public sealed class AgentConnectionTests : IAsyncDisposable
{
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(15);
    private static readonly AgentIdentity _identity = new("agent-test", Environment.ProcessId, Elevated: false, "build-x", VulkanLayerRegistered: false, CpuTempAvailable: false);
    private static readonly AgentConnectionOptions _fast = new()
    {
        ConnectAttempts = 3,
        ConnectBackoff = TimeSpan.FromMilliseconds(30),
        ConnectTimeout = TimeSpan.FromMilliseconds(300),
        RetryInterval = TimeSpan.FromMilliseconds(200),
        Keepalive = TimeSpan.FromMilliseconds(400),
        RequestTimeout = TimeSpan.FromSeconds(5),
    };

    private readonly string _pipeName = "FrameLedger.test." + Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _stop = new();
    private readonly List<AgentConnectionState> _seen = [];
    private PipeServer? _server;
    private Task? _serving;
    private CancellationTokenSource? _serverStop;
    private AgentConnection? _connection;
    private Task? _running;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A launcher that can be told whether an Agent is beside the App, and whose start brings the server up.</summary>
    private sealed class FakeLauncher(Func<bool> start) : IAgentLauncher
    {
        public bool CanLaunch { get; set; }

        /// <summary>What <see cref="IAgentLauncher.RunningAgent"/> answers: an Agent that is up, whether or not it answers.</summary>
        public string? Running { get; set; }

        public string? RunningAgent => Running;

        public int Starts { get; private set; }

        public bool TryStart()
        {
            Starts++;
            return start();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        if (_running is not null)
        {
            Task running = _running;
            try
            {
                await running.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        await StopServerAsync().ConfigureAwait(false);
        _stop.Dispose();
    }

    private void StartServer()
    {
        _serverStop = new CancellationTokenSource();
        _server = new PipeServer(new PipeServerOptions { PipeName = _pipeName },
            new AgentRequestHandler(_identity, static () => "l1", static () => AgentStatus.Idle));
        _serving = _server.RunAsync(_serverStop.Token);
    }

    private async Task StopServerAsync()
    {
        if (_serverStop is null)
        {
            return;
        }

        await _serverStop.CancelAsync().ConfigureAwait(false);
        Task? serving = _serving;
        if (serving is not null)
        {
            try
            {
                await serving.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _server?.Dispose();
        _serverStop.Dispose();
        _serverStop = null;
        _server = null;
        _serving = null;
    }

    private AgentConnection Start(FakeLauncher launcher, bool holdLaunches = false)
    {
        _connection = new AgentConnection(launcher, () => new PipeClient(_pipeName), _fast, "app-test");
        _connection.SetLaunchHold(holdLaunches);
        _connection.Changed += (s, _) =>
        {
            lock (_seen)
            {
                _seen.Add(((AgentConnection)s!).State);
            }
        };
        _running = _connection.RunAsync(_stop.Token);
        return _connection;
    }

    private static async Task WaitForAsync(Func<bool> condition, string because)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + _wait;
        while (!condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(25, Ct).ConfigureAwait(false);
        }

        condition().Should().BeTrue(because);
    }

    [Fact]
    public async Task WhileLaunchesAreHeldAnAbsentAgentIsNotStartedAndIsStartedOnceReleased()
    {
        // P4 PR-5: the update's apply asks the Agent to stop and must not have this loop start it again under the updater.
        FakeLauncher? launcher = null;
        launcher = new FakeLauncher(() =>
        {
            StartServer();
            return true;
        })
        { CanLaunch = true };
        AgentConnection c = Start(launcher, holdLaunches: true);

        await WaitForAsync(() => c.Rounds >= 2, "two rounds found nothing").ConfigureAwait(true);
        launcher.Starts.Should().Be(0, "held");
        c.LaunchesHeld.Should().BeTrue();
        lock (_seen)
        {
            _seen.Should().NotContain(AgentConnectionState.Starting, "a held round never says it is starting one");
            _seen.Should().NotContain(AgentConnectionState.Missing, "an Agent could be started, so the pill says Offline, not Missing");
        }

        c.SetLaunchHold(false);
        c.RetryNow();
        await WaitForAsync(() => c.State == AgentConnectionState.Connected, "released: started, then connected").ConfigureAwait(true);
        launcher.Starts.Should().Be(1);
    }

    [Fact]
    public async Task WithNoAgentBesideTheAppTheStateIsMissingAndNothingIsStarted()
    {
        var launcher = new FakeLauncher(static () => false) { CanLaunch = false };
        AgentConnection c = Start(launcher);

        await WaitForAsync(() => c.State == AgentConnectionState.Missing, "three attempts × 30 ms, nothing to start").ConfigureAwait(true);
        await WaitForAsync(() => c.Rounds >= 2, "rounds continue in case an Agent appears").ConfigureAwait(true);
        launcher.Starts.Should().Be(0);
        c.Hello.Should().BeNull();
    }

    [Fact]
    public async Task ARunningAgentIsFoundHelloedAndReportedConnected()
    {
        StartServer();
        var launcher = new FakeLauncher(static () => false) { CanLaunch = true };
        AgentConnection c = Start(launcher);

        await WaitForAsync(() => c.State == AgentConnectionState.Connected, "the server is up").ConfigureAwait(true);
        c.Hello!.AgentVersion.Should().Be("agent-test");
        c.Status!.State.Should().Be(AgentStatus.IdleState);
        launcher.Starts.Should().Be(0, "an answering Agent is never restarted");

        // Keepalive: after a few intervals the connection is still connected (a ping that failed would drop it).
        await Task.Delay(_fast.Keepalive * 3, Ct).ConfigureAwait(true);
        c.State.Should().Be(AgentConnectionState.Connected);
        (await c.RefreshStatusAsync(Ct).ConfigureAwait(true)).Should().NotBeNull();
    }

    [Fact]
    public async Task WhenTheAgentIsAbsentItIsStartedFromBesideTheAppAndThenConnected()
    {
        // The fake launcher's "start" brings the server up, the way FrameLedger.Agent.exe --serve would.
        FakeLauncher? launcher = null;
        launcher = new FakeLauncher(() =>
        {
            StartServer();
            return true;
        })
        { CanLaunch = true };
        AgentConnection c = Start(launcher);

        await WaitForAsync(() => c.State == AgentConnectionState.Connected, "started, then connected on the second try").ConfigureAwait(true);
        launcher.Starts.Should().Be(1);
        c.Launches.Should().Be(1);
        lock (_seen)
        {
            _seen.Should().Contain(AgentConnectionState.Starting, "the pill said so on the way");
        }
    }

    [Fact]
    public async Task AnAgentThatIsUpButDoesNotAnswerIsReportedAndNeverStartedAgain()
    {
        // 2026-09-17: an elevated App was refused by the pipe of the Agent already serving, took the refusal for an absent
        // Agent, and started one per round; four Agents recorded every session four times. Here nothing answers, the launcher
        // knows an Agent holds the data folder, and a start would bring a server up — so it must never be called.
        FakeLauncher? launcher = null;
        launcher = new FakeLauncher(() =>
        {
            StartServer();
            return true;
        })
        { CanLaunch = true, Running = "an Agent already holds the test folder" };
        AgentConnection c = Start(launcher);

        await WaitForAsync(() => c.Rounds >= 3, "three rounds found no pipe").ConfigureAwait(true);
        launcher.Starts.Should().Be(0, "one Agent per data folder, however many rounds fail");
        c.Launches.Should().Be(0);
        c.LastConnectFailure.Should().StartWith(nameof(TimeoutException), "why the round failed is kept, not swallowed");
        lock (_seen)
        {
            _seen.Should().NotContain(AgentConnectionState.Starting);
        }

        launcher.Running = null;
        c.RetryNow();
        await WaitForAsync(() => c.State == AgentConnectionState.Connected, "that Agent is gone: this App starts one and connects").ConfigureAwait(true);
        launcher.Starts.Should().Be(1);
        c.LastConnectFailure.Should().BeNull();
    }

    [Fact]
    public void TheLauncherSeesAnAgentHoldingItsDataFolderWhoeverStartedIt()
    {
        string folder = Path.Combine(Path.GetTempPath(), "fl-launcher-" + Guid.NewGuid().ToString("N"));
        var launcher = new AgentLauncher(folder);
        launcher.RunningAgent.Should().BeNull("nothing holds a folder named for this test, and this launcher started nothing");

        using (AgentInstanceLock? held = AgentInstanceLock.TryAcquire(folder))
        {
            held.Should().NotBeNull();
            launcher.RunningAgent.Should().Contain(folder, "the logon task's Agent, a console's or another App's — the claim is the same");
        }

        launcher.RunningAgent.Should().BeNull("the claim went with its handle");
    }

    [Fact]
    public async Task ALostAgentIsOfflineAndAReturningOneIsConnectedAgain()
    {
        StartServer();
        var launcher = new FakeLauncher(static () => false) { CanLaunch = true };
        AgentConnection c = Start(launcher);
        await WaitForAsync(() => c.State == AgentConnectionState.Connected, "up").ConfigureAwait(true);

        await StopServerAsync().ConfigureAwait(true);
        await WaitForAsync(() => c.State is AgentConnectionState.Offline or AgentConnectionState.Connecting or AgentConnectionState.Starting, "the pipe dropped").ConfigureAwait(true);
        await WaitForAsync(() => c.Rounds >= 1, "a new round ran").ConfigureAwait(true);

        StartServer();
        c.RetryNow();
        await WaitForAsync(() => c.State == AgentConnectionState.Connected && c.Hello is not null, "back").ConfigureAwait(true);
    }
}
