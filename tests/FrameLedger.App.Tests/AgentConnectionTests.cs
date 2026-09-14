using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Application.Ipc;
using FrameLedger.Infrastructure.Ipc;
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

    private AgentConnection Start(FakeLauncher launcher)
    {
        _connection = new AgentConnection(launcher, () => new PipeClient(_pipeName), _fast, "app-test");
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
