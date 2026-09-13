// The message set of 07_IPC §Messages — the READ half as of P3 PR-1 (2026-09-13): Hello/GetStatus/Ping and
// the Agent → UI events. The command half (SetWatchlist, SetHookEnabled, LaunchGame, Pause/Resume, StopSession,
// UpdateRules, Shutdown) is PR-1b (HANDOFF §P3), and none of it is declared here until it has a handler — a
// declared message nobody answers is the §S29(c) shape.
//
// ONE FILE FOR THE WHOLE SET, deliberately (MA0048 is suppressed for this project, see the csproj): the types
// are one wire contract read against one table in 07_IPC, and the property names ARE the JSON — camelCase by the
// context's naming policy, nulls omitted. Rename a property and the other process stops understanding it.
//
// Every value that is not measured is null, never 0 (FR-4.9 at the wire).

namespace FrameLedger.Shared.Ipc;

/// <summary>The <c>type</c> discriminators, one per row of <c>07_IPC</c> §Messages.</summary>
public static class IpcMessageType
{
    public const string Hello = "Hello";
    public const string HelloAck = "HelloAck";
    public const string GetStatus = "GetStatus";
    public const string StatusAck = "StatusAck";
    public const string Ping = "Ping";
    public const string Pong = "Pong";

    /// <summary>The Agent's answer when it cannot answer: unknown type, malformed payload, protocol mismatch.</summary>
    public const string Error = "Error";

    public const string SessionStarted = "SessionStarted";
    public const string SessionProgress = "SessionProgress";
    public const string SessionCompleted = "SessionCompleted";
    public const string CaptureRefused = "CaptureRefused";
    public const string CaptureDegraded = "CaptureDegraded";
    public const string SafetyUnhook = "SafetyUnhook";
    public const string CaptureError = "CaptureError";
}

/// <summary>Codes an <see cref="ErrorAck"/> carries.</summary>
public static class IpcErrorCode
{
    public const string UnknownType = "UnknownType";
    public const string Malformed = "Malformed";
    public const string ProtocolMismatch = "ProtocolMismatch";
    public const string HandlerFaulted = "HandlerFaulted";
}

/// <summary>Codes a <see cref="CaptureErrorEvent"/> carries (<c>07_IPC</c> §Messages, <c>CaptureError</c>).</summary>
public static class CaptureErrorCode
{
    public const string InjectFailed = "InjectFailed";
    public const string RingVersionMismatch = "RingVersionMismatch";
    public const string AttachRefused = "AttachRefused";
    public const string TelemetryUnavailable = "TelemetryUnavailable";
    public const string DbWriteFailed = "DbWriteFailed";

    /// <summary>The session's task threw before it could finalize; the <c>.partial</c> stays for recovery.</summary>
    public const string SessionFaulted = "SessionFaulted";
}

/// <summary><c>Hello</c>: the client names itself and the protocol it speaks.</summary>
public sealed record HelloRequest(string AppVersion, int Protocol);

/// <summary>
/// <c>HelloAck</c>: what this Agent is. <c>etwAvailable</c> from the original table is gone with the ETW tier
/// (2026-08-28); <c>VulkanLayerRegistered</c> is false by construction today — the layer is handed to a launched
/// process through <c>VK_ADD_IMPLICIT_LAYER_PATH</c>, never registered machine-wide (P1 item 3).
/// </summary>
public sealed record HelloAck(
    string AgentVersion,
    int Protocol,
    int Pid,
    bool Elevated,
    string? OverlayBuildId,
    bool VulkanLayerRegistered,
    string? TelemetrySource,
    bool CpuTempAvailable);

/// <summary><c>GetStatus</c>: no payload.</summary>
public sealed record GetStatusRequest;

/// <summary>One session the Agent is running right now. Tier 1 once the ring is attached, 2 until then or never.</summary>
public sealed record ActiveSession(Guid SessionGuid, long GameId, string? GameName, int Pid, int Tier, DateTimeOffset StartedAt);

/// <summary>
/// <c>StatusAck</c>. <see cref="State"/> is <c>idle</c>, <c>recording</c> (a session runs, nothing hooked) or
/// <c>capturing</c> (at least one ring attached); <see cref="ActiveSession"/> is the hooked one when there is one.
/// </summary>
public sealed record StatusAck(string State, ActiveSession? ActiveSession, int? Tier, IReadOnlyList<ActiveSession> ActiveSessions);

public sealed record PingRequest;

public sealed record PongAck;

public sealed record ErrorAck(string Code, string Message);

/// <summary>Published when the ring is attached — the moment a session becomes Tier 1 and the live card has something to show.</summary>
public sealed record SessionStartedEvent(Guid SessionGuid, long GameId, string? GameName, int Pid, int Tier, DateTimeOffset StartedAt);

/// <summary>
/// 1 Hz while a hooked session runs and a client is connected (<c>04_CAPTURE</c> §Live progress). A 5 s rolling
/// window over the ring's records, computed by the same Domain calculators the stored row is — never a second
/// implementation of a metric.
/// </summary>
/// <remarks>
/// CLAUDE.md rule 6 at the wire: <see cref="NativeFps5s"/>, <see cref="DisplayedFps5s"/> and <see cref="FgFactor"/>
/// are set only when frame generation was MEASURED in the window; otherwise the one number that may stand alone is
/// <see cref="PresentedFps5s"/>, and <see cref="PresentedQualifier"/> is mandatory beside it.
/// </remarks>
public sealed record SessionProgressEvent
{
    public required Guid SessionGuid { get; init; }

    /// <summary>Seconds since the ring was attached.</summary>
    public required double ElapsedS { get; init; }

    /// <summary>Presents in the window.</summary>
    public required int Presents5s { get; init; }

    public double? PresentedFps5s { get; init; }

    /// <summary><c>census_not_run</c> · <c>no_fg_runtime</c> · <c>fg_runtime_loaded</c> · <c>none_withheld</c>.</summary>
    public required string PresentedQualifier { get; init; }

    public double? NativeFps5s { get; init; }

    public double? DisplayedFps5s { get; init; }

    public double? FgFactor { get; init; }

    /// <summary>The row's vocabulary: <c>na</c>, <c>none</c>, <c>dlssg</c>, <c>fsrfg</c>, <c>xefg</c>, <c>unknown</c>.</summary>
    public required string FgMode { get; init; }

    public string? Upscaler { get; init; }

    /// <summary>The vendor's own preset value, as the row stores it; null when no record carried params.</summary>
    public string? UpscalerQuality { get; init; }

    public int? RenderW { get; init; }

    public int? RenderH { get; init; }

    public int? OutputW { get; init; }

    public int? OutputH { get; init; }

    /// <summary>Null when no record in the window claimed a ray-tracing measurement.</summary>
    public bool? RtActive { get; init; }

    public double? GpuTempC { get; init; }

    /// <summary>Null until the Agent composes a CPU sensor (it does not, unelevated).</summary>
    public double? CpuTempC { get; init; }

    public int? VramProcMb { get; init; }

    public int? LatencyUs { get; init; }
}

/// <summary>
/// The session is over and, when <see cref="SessionId"/> is set, in SQLite; the UI loads the row from there
/// (<c>07_IPC</c> §Client behavior). <see cref="Finalize"/> is <c>saved</c> or <c>discarded</c>.
/// </summary>
public sealed record SessionCompletedEvent(Guid SessionGuid, long? SessionId, string ExitStatus, int Tier, string Finalize, string Reason);

/// <summary>The gate or the guard said no before anything was injected (<c>08_UI</c> §Safety events: never a toast).</summary>
public sealed record CaptureRefusedEvent(long GameId, string? GameName, string Reason, string? Family, string? Signal);

/// <summary>Measurement STOPPED mid-session (two-rung ladder: there is no lower fidelity to continue at).</summary>
public sealed record CaptureDegradedEvent(Guid SessionGuid, int From, int To, string Reason);

/// <summary>Anti-cheat appeared mid-session and our own guard published the stop.</summary>
public sealed record SafetyUnhookEvent(Guid SessionGuid, string? Family, string? Signal);

public sealed record CaptureErrorEvent(Guid? SessionGuid, string Code, string Message);
