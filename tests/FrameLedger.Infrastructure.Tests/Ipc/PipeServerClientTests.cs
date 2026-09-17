using System.IO.Pipes;
using System.Security.AccessControl;
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

        server.SecurityDescriptor.Should().Be($"O:{me.Value}D:P(A;;GA;;;{me.Value})(A;;GA;;;BA)");
        PipeAccessControl.DescriptorBytes(server.SecurityDescriptor).Should().NotBeEmpty("the SDDL must parse into a binary descriptor");
        server.PipeName.Should().Be(_pipeName);
    }

    [Fact]
    public async Task ThePipeIsOwnedByThisUserWhateverTheTokensDefaultOwner()
    {
        // 2026-09-17: left to default, the owner of an elevated Agent's pipe is Administrators (on CI's elevated runner,
        // this test's), and a client checking the owner refuses the same user's Agent across elevation.
        Start();
        await using var raw = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await raw.ConnectAsync(_timeout, Ct).ConfigureAwait(true);

        PipeAccessControl.OwnerOf(raw).Should().Be(PipeAccessControl.CurrentUser());
        (await ConnectAsync().ConfigureAwait(true)).IsConnected.Should().BeTrue("the client's own owner check passes");
    }

    [Fact]
    public async Task ATokenThatOwnsAsAdministratorsTrustsItsUsersPipeAndRefusesAnAdministratorsOne()
    {
        // The morning of 2026-09-17, as a test: an App started as administrator, whose token's default owner is
        // Administrators, and the user's Agent, whose pipe the user owns. PipeOptions.CurrentUserOnly compared the two and
        // refused; the check is the token USER. Only such a token can build the second pipe, which is why this needs one —
        // CI's runner is elevated; an unelevated run has nothing to show here.
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        Assert.SkipUnless(identity.Owner == administrators, "this token's default owner is its user, not Administrators (an unelevated run)");

        (await ConnectToAPipeOwnedByAsync(PipeAccessControl.CurrentUser()).ConfigureAwait(true))
            .Should().BeNull("the user's own Agent, reached from an elevated App");
        UnauthorizedAccessException? refused = await ConnectToAPipeOwnedByAsync(administrators).ConfigureAwait(true);
        refused.Should().NotBeNull("a pipe the user does not own is not the user's Agent, elevated or not");
        refused!.Message.Should().Contain(administrators.Value);
    }

    [Fact]
    public async Task AnInstanceThatCannotBeCreatedIsRetriedWithBackoffAndLoggedOnceWithItsError()
    {
        // 2026-09-17: three extra Agents could not create an instance of a name whose limit a fourth had reached, and each
        // logged "accept failed" every 260 ms for four hours, without the error. Here the first server takes the only instance.
        Start(maxClients: 1);
        var lines = new List<string>();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        using var second = new PipeServer(new PipeServerOptions { PipeName = _pipeName, MaxClients = 1 },
            new AgentRequestHandler(_identity, static () => "l1", () => _status), lines.Add);
        Task running = second.RunAsync(stop.Token);

        await Task.Delay(TimeSpan.FromSeconds(3), Ct).ConfigureAwait(true);
        await stop.CancelAsync().ConfigureAwait(true);
        try
        {
            await running.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }

        lines.Should().ContainSingle("250 ms doubling is four attempts in three seconds, and only the first is logged; the fixed 250 ms loop wrote twelve")
            .Which.Should().Contain("(error 231)", "ERROR_PIPE_BUSY: the name's instances are all taken");
    }

    /// <summary>A <see cref="PipeClient"/> against a bare pipe owned by <paramref name="owner"/>: its refusal, or null when it connected.</summary>
    private async Task<UnauthorizedAccessException?> ConnectToAPipeOwnedByAsync(SecurityIdentifier owner)
    {
        string name = _pipeName + ".owned";
        var security = new PipeSecurity();
        security.SetOwner(owner);
        security.AddAccessRule(new PipeAccessRule(PipeAccessControl.CurrentUser(), PipeAccessRights.FullControl, AccessControlType.Allow));
        NamedPipeServerStream server = NamedPipeServerStreamAcl.Create(
            name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
        await using (server.ConfigureAwait(false))
        {
            Task accepted = server.WaitForConnectionAsync(Ct);
            var client = new PipeClient(name);
            await using (client.ConfigureAwait(false))
            {
                try
                {
                    await client.ConnectAsync(_timeout, Ct).ConfigureAwait(false);
                    return null;
                }
                catch (UnauthorizedAccessException ex)
                {
                    return ex;
                }
                finally
                {
                    await accepted.ConfigureAwait(false);
                }
            }
        }
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
