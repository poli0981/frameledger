namespace FrameLedger.Shared.Ipc;

/// <summary>
/// The constants of channel C (<c>07_IPC</c> §C — command pipe): one pipe name, one protocol number, one frame
/// cap, one client limit. Both processes compile against this file, so a mismatch is a build, not a runtime.
/// </summary>
/// <remarks>
/// <c>protocol</c> bumps only on a breaking change; additive fields are always allowed and unknown fields are
/// ignored on both sides (<c>07_IPC</c> §Versioning, pinned by <c>IpcCodecTests</c>). The pipe name carries the
/// protocol's major so two incompatible Agents can never answer the same client.
/// </remarks>
public static class IpcProtocol
{
    /// <summary>The message set's version; a client sends it in <c>Hello</c> and the Agent refuses another.</summary>
    public const int Version = 2;

    /// <summary>The pipe's name under <c>\\.\pipe\</c>. Plain and identifiable, like the ring's (<c>19_SAFETY</c>).</summary>
    public const string PipeName = "FrameLedger.v2";

    /// <summary>Framing cap: 4-byte little-endian length + UTF-8 JSON, at most this many body bytes.</summary>
    public const int MaxFrameBytes = 1024 * 1024;

    /// <summary>The length prefix, little-endian, unsigned.</summary>
    public const int HeaderBytes = 4;

    /// <summary>The UI and a future CLI; a third connection waits for one of them to leave.</summary>
    public const int MaxClients = 2;

    /// <summary>The client's keepalive cadence (<c>Ping</c> → <c>Pong</c>).</summary>
    public static readonly TimeSpan Keepalive = TimeSpan.FromSeconds(15);
}
