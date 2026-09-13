using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Ipc;

/// <summary>
/// The read half of <c>07_IPC</c> §Messages: <c>Hello</c>, <c>GetStatus</c>, <c>Ping</c>. Nothing here changes
/// a state — the command half (PR-1b) is where <c>07_IPC</c> §The pipe is not a trust boundary starts to bite.
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

    public AgentRequestHandler(AgentIdentity identity, Func<string?> telemetryDescriptor, Func<AgentStatus> status)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _telemetryDescriptor = telemetryDescriptor ?? throw new ArgumentNullException(nameof(telemetryDescriptor));
        _status = status ?? throw new ArgumentNullException(nameof(status));
    }

    public byte[] Handle(IpcEnvelope request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Type switch
        {
            IpcMessageType.Hello => Hello(request),
            IpcMessageType.GetStatus => IpcCodec.Encode(IpcMessageType.StatusAck, request.Id, _status().ToAck()),
            IpcMessageType.Ping => IpcCodec.Encode(IpcMessageType.Pong, request.Id, new PongAck()),
            _ => IpcCodec.Encode(IpcMessageType.Error, request.Id,
                new ErrorAck(IpcErrorCode.UnknownType, $"'{request.Type}' is not a request this Agent answers (07_IPC §Messages, read half)")),
        };
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
            _identity.CpuTempAvailable));
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
