using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Ipc;

/// <summary>
/// The read half of <c>07_IPC</c> §Messages — <c>Hello</c>, <c>GetStatus</c>, <c>Ping</c> — and the front door
/// for the command half: anything else is offered to <see cref="AgentCommandHandler"/> when one is composed, and
/// answered <c>Error UnknownType</c> otherwise.
/// </summary>
/// <remarks>
/// The telemetry descriptor is a function because composing the layers costs a library start; the identity is a
/// value because it never changes; the status is a function because it changes every second.
/// </remarks>
public sealed class AgentRequestHandler : IIpcRequestHandler
{
    private readonly AgentIdentity _identity;
    private readonly Func<string?> _telemetryDescriptor;
    private readonly Func<AgentStatus> _status;
    private readonly Func<AgentCommandHandler?> _commands;

    /// <summary>
    /// <c>commands</c> is a function, resolved on the first command rather than at construction: the command
    /// handler reaches the orchestrator, which reaches the recorder, whose observer publishes through the pipe
    /// server that owns this handler — a cycle a container cannot build eagerly, and one a request can walk lazily.
    /// </summary>
    public AgentRequestHandler(AgentIdentity identity, Func<string?> telemetryDescriptor, Func<AgentStatus> status, Func<AgentCommandHandler?>? commands = null)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _telemetryDescriptor = telemetryDescriptor ?? throw new ArgumentNullException(nameof(telemetryDescriptor));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _commands = commands ?? (static () => null);
    }

    public async ValueTask<byte[]> HandleAsync(IpcEnvelope request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        switch (request.Type)
        {
            case IpcMessageType.Hello:
                return Hello(request);
            case IpcMessageType.GetStatus:
                return IpcCodec.Encode(IpcMessageType.StatusAck, request.Id, _status().ToAck(_commands()?.IsPaused ?? false));
            case IpcMessageType.Ping:
                return IpcCodec.Encode(IpcMessageType.Pong, request.Id, new PongAck());
            default:
                AgentCommandHandler? commands = _commands();
                byte[]? command = commands is null ? null : await commands.HandleAsync(request, ct).ConfigureAwait(false);
                return command ?? IpcCodec.Encode(IpcMessageType.Error, request.Id,
                    new ErrorAck(IpcErrorCode.UnknownType, $"'{request.Type}' is not a request this Agent answers (07_IPC §Messages)"));
        }
    }

    private byte[] Hello(IpcEnvelope request)
    {
        HelloRequest? hello = IpcCodec.Payload<HelloRequest>(request);
        if (hello is null || hello.Protocol != IpcProtocol.Version)
        {
            return IpcCodec.Encode(IpcMessageType.Error, request.Id, new ErrorAck(IpcErrorCode.ProtocolMismatch,
                $"this Agent speaks protocol {IpcProtocol.Version}; the client sent {hello?.Protocol.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "no Hello payload"}"));
        }

        return IpcCodec.Encode(IpcMessageType.HelloAck, request.Id, new HelloAck(
            _identity.AgentVersion,
            IpcProtocol.Version,
            _identity.Pid,
            _identity.Elevated,
            _identity.OverlayBuildId,
            _identity.VulkanLayerRegistered,
            Descriptor(),
            _identity.CpuTempAvailable,
            _identity.DisclosureVersion));
    }

    /// <summary>Null when the layers cannot be composed at all — an absent descriptor, never an invented one.</summary>
    private string? Descriptor()
    {
        try
        {
            return _telemetryDescriptor();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }
}
