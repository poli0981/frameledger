using System.Security.Principal;
using System.Text;
using FluentAssertions;
using FluentAssertions.Specialized;
using FrameLedger.Application.Ipc;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Infrastructure.Tests.Ipc;

/// <summary>
/// The pipe end to end, in one process, on a pipe named for this test alone: the SDDL the instance is created
/// with, the request/ack round trip through the real handler, an event reaching the client, the client cap, a
/// malformed frame, and a stranger refused — every refusal driven, not described.
/// </summary>
public sealed class PipeServerClientTests : IAsyncDisposable
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
    private static readonly AgentIdentity _identity = new("0.1.0-test", Environment.ProcessId, Elevated: false, "build-abc", VulkanLayerRegistered: false, CpuTempAvailable: false);

    private readonly string _pipeName = "FrameLedger.test." + Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _stop = new();
    private readonly List<string> _log = [];
    private readonly List<PipeClient> _clients = [];
    private PipeServer? _server;
    private Task? _serving;
    private AgentStatus _status = AgentStatus.Idle;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        foreach (PipeClient client in _clients)
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }

        await _stop.CancelAsync().ConfigureAwait(false);
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
        _stop.Dispose();
    }

    private PipeServer Start(Func<System.IO.Pipes.NamedPipeServerStream, SecurityIdentifier?>? clientUserOf = null, int maxClients = IpcProtocol.MaxClients)
    {
        var handler = new AgentRequestHandler(_identity, static () => "l1+lhm", () => _status);
        _server = new PipeServer(new PipeServerOptions { PipeName = _pipeName, MaxClients = maxClients }, handler, _log.Add, clientUserOf);
        _serving = _server.RunAsync(_stop.Token);
        return _server;
    }

    /// <summary>A client this fixture owns and disposes; a test never has to <c>await using</c> one.</summary>
    private async Task<PipeClient> ConnectAsync(TimeSpan? timeout = null)
    {
        var client = new PipeClient(_pipeName);
        _clients.Add(client);
        await client.ConnectAsync(timeout ?? _timeout, Ct).ConfigureAwait(false);
        return client;
    }

    [Fact]
    public void TheDescriptorNamesThisUserAndAdministratorsAndNobodyElse()
    {
        PipeServer server = Start();
        SecurityIdentifier me = PipeAccessControl.CurrentUser();

        server.SecurityDescriptor.Should().Be($"D:P(A;;GA;;;{me.Value})(A;;GA;;;BA)");
        PipeAccessControl.DescriptorBytes(server.SecurityDescriptor).Should().NotBeEmpty("the SDDL must parse into a binary descriptor");
        server.PipeName.Should().Be(_pipeName);
    }

    [Fact]
    public async Task HelloGetStatusAndPingRoundTripThroughTheRealHandler()
    {
        PipeServer server = Start();
        PipeClient client = await ConnectAsync().ConfigureAwait(true);

        HelloAck hello = await client.HelloAsync("app-1.0", _timeout, Ct).ConfigureAwait(true);
        hello.Protocol.Should().Be(IpcProtocol.Version);
        hello.AgentVersion.Should().Be("0.1.0-test");
        hello.OverlayBuildId.Should().Be("build-abc");
        hello.TelemetrySource.Should().Be("l1+lhm");
        hello.Pid.Should().Be(Environment.ProcessId);

        StatusAck status = await client.GetStatusAsync(_timeout, Ct).ConfigureAwait(true);
        status.State.Should().Be(AgentStatus.IdleState);
        status.ActiveSession.Should().BeNull();

        _status = new AgentStatus([new ActiveSession(Guid.NewGuid(), 7, "Title", 4242, 1, DateTimeOffset.UtcNow)]);
        status = await client.GetStatusAsync(_timeout, Ct).ConfigureAwait(true);
        status.State.Should().Be(AgentStatus.CapturingState);
        status.ActiveSession!.Pid.Should().Be(4242);
        status.Tier.Should().Be(1);

        (await client.PingAsync(_timeout, Ct).ConfigureAwait(true)).Should().NotBeNull();
        server.ConnectedClients.Should().Be(1);
        server.RejectedClients.Should().Be(0);
    }

    [Fact]
    public async Task AnUnknownRequestTypeAndAWrongProtocolAreAnsweredWithErrorNotSilence()
    {
        Start();
        PipeClient client = await ConnectAsync().ConfigureAwait(true);

        Func<Task> unknown = async () => await client.RequestAsync<PingRequest, PongAck>("SetHookEnabled", new PingRequest(), IpcMessageType.Pong, _timeout, Ct).ConfigureAwait(true);
        (await unknown.Should().ThrowAsync<IpcRequestException>().ConfigureAwait(true)).Which.Code.Should().Be(IpcErrorCode.UnknownType,
            "the command half is PR-1b; until then the type is unknown, and the client is told so");

        Func<Task> old = async () => await client.RequestAsync<HelloRequest, HelloAck>(IpcMessageType.Hello, new HelloRequest("app", 1), IpcMessageType.HelloAck, _timeout, Ct).ConfigureAwait(true);
        (await old.Should().ThrowAsync<IpcRequestException>().ConfigureAwait(true)).Which.Code.Should().Be(IpcErrorCode.ProtocolMismatch);

        // The connection survives both: the next well-formed request is answered.
        (await client.PingAsync(_timeout, Ct).ConfigureAwait(true)).Should().NotBeNull();
    }

    [Fact]
    public async Task AMalformedFrameIsAnsweredWithErrorMalformedAndTheConnectionSurvives()
    {
        Start();
        PipeClient client = await ConnectAsync().ConfigureAwait(true);
        await client.HelloAsync("app", _timeout, Ct).ConfigureAwait(true);

        await client.SendAsync(Encoding.UTF8.GetBytes("this is not json"), Ct).ConfigureAwait(true);

        // No id could be read, so the Error arrives as an event.
        IpcEnvelope error = await client.Events.ReadAsync(Ct).AsTask().WaitAsync(_timeout, Ct).ConfigureAwait(true);
        error.Type.Should().Be(IpcMessageType.Error);
        IpcCodec.Payload<ErrorAck>(error)!.Code.Should().Be(IpcErrorCode.Malformed);
        (await client.PingAsync(_timeout, Ct).ConfigureAwait(true)).Should().NotBeNull();
    }

    [Fact]
    public async Task APublishedEventReachesEveryConnectedClientAndNobodyWhenThereIsNone()
    {
        PipeServer server = Start();
        server.HasClients.Should().BeFalse();
        server.Publish(IpcMessageType.SafetyUnhook, new SafetyUnhookEvent(Guid.NewGuid(), "eac", "x.dll"));

        PipeClient a = await ConnectAsync().ConfigureAwait(true);
        PipeClient b = await ConnectAsync().ConfigureAwait(true);
        await a.HelloAsync("a", _timeout, Ct).ConfigureAwait(true);
        await b.HelloAsync("b", _timeout, Ct).ConfigureAwait(true);
        server.HasClients.Should().BeTrue();
        server.ConnectedClients.Should().Be(2);

        var guid = Guid.NewGuid();
        server.Publish(IpcMessageType.SessionStarted, new SessionStartedEvent(guid, 7, "Title", 4242, 1, DateTimeOffset.UtcNow));

        foreach (PipeClient client in new[] { a, b })
        {
            IpcEnvelope e = await client.Events.ReadAsync(Ct).AsTask().WaitAsync(_timeout, Ct).ConfigureAwait(true);
            e.Type.Should().Be(IpcMessageType.SessionStarted);
            e.IsEvent.Should().BeTrue();
            IpcCodec.Payload<SessionStartedEvent>(e)!.SessionGuid.Should().Be(guid);
        }
    }

    [Fact]
    public async Task AThirdClientWaitsUntilOneOfTheTwoLeaves()
    {
        PipeServer server = Start();
        PipeClient a = await ConnectAsync().ConfigureAwait(true);
        PipeClient b = await ConnectAsync().ConfigureAwait(true);
        await a.HelloAsync("a", _timeout, Ct).ConfigureAwait(true);
        await b.HelloAsync("b", _timeout, Ct).ConfigureAwait(true);

        Func<Task> third = async () => await ConnectAsync(TimeSpan.FromMilliseconds(700)).ConfigureAwait(true);
        await third.Should().ThrowAsync<TimeoutException>("07_IPC §C: max 2 clients, and no third instance exists to connect to").ConfigureAwait(true);

        await a.DisposeAsync().ConfigureAwait(true);
        PipeClient c = await ConnectAsync().ConfigureAwait(true);
        (await c.PingAsync(_timeout, Ct).ConfigureAwait(true)).Should().NotBeNull();
        server.ConnectedClients.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task AClientWhoseTokenUserIsNotOursIsDisconnectedWithoutAnAnswer()
    {
        // The identity seam returns "Everyone" for the connecting client; the real check (impersonation) is
        // exercised by every other case here, where the client is this same user.
        PipeServer server = Start(clientUserOf: static _ => new SecurityIdentifier(WellKnownSidType.WorldSid, null));
        PipeClient stranger = await ConnectAsync().ConfigureAwait(true);

        Func<Task> hello = async () => await stranger.HelloAsync("x", _timeout, Ct).ConfigureAwait(true);
        ExceptionAssertions<Exception> thrown = await hello.Should().ThrowAsync<Exception>().ConfigureAwait(true);
        (thrown.Which is IOException or TimeoutException).Should().BeTrue("the first frame is read, the user compared, and the pipe closed; got {0}", thrown.Which.GetType().Name);

        await WaitUntilAsync(() => server.RejectedClients == 1).ConfigureAwait(true);
        server.RejectedClients.Should().Be(1);
        _log.Should().Contain(static l => l.Contains("refused", StringComparison.Ordinal));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + _timeout;
        while (!condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(25, Ct).ConfigureAwait(false);
        }
    }
}
