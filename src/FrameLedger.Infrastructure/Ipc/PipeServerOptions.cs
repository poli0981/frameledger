using FrameLedger.Shared.Ipc;

namespace FrameLedger.Infrastructure.Ipc;

/// <summary>Knobs on <see cref="PipeServer"/>. The product uses every default; a test names its own pipe.</summary>
public sealed record PipeServerOptions
{
    /// <summary>Under <c>\\.\pipe\</c>. The product's is <see cref="IpcProtocol.PipeName"/>.</summary>
    public string PipeName { get; init; } = IpcProtocol.PipeName;

    /// <summary><c>07_IPC</c> §C: two — the UI and a future CLI.</summary>
    public int MaxClients { get; init; } = IpcProtocol.MaxClients;

    /// <summary>
    /// Frames queued per client before the oldest is dropped. The publisher never waits on a client
    /// (<c>04_CAPTURE</c> §Threading model); a client that cannot keep up loses events, counted per client.
    /// </summary>
    public int OutboundQueueCapacity { get; init; } = 256;
}
