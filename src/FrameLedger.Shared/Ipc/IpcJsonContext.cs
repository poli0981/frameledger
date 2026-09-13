using System.Text.Json.Serialization;

namespace FrameLedger.Shared.Ipc;

/// <summary>
/// Source-generated JSON for the pipe (<c>07_IPC</c> §C: "System.Text.Json source-generated contexts in
/// <c>FrameLedger.Shared</c>"). No reflection, camelCase on the wire, nulls omitted, unknown members skipped —
/// the last is what makes additive fields safe across versions, and <c>IpcCodecTests</c> pins it both ways.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    WriteIndented = false)]
[JsonSerializable(typeof(IpcEnvelope))]
[JsonSerializable(typeof(HelloRequest))]
[JsonSerializable(typeof(HelloAck))]
[JsonSerializable(typeof(GetStatusRequest))]
[JsonSerializable(typeof(StatusAck))]
[JsonSerializable(typeof(ActiveSession))]
[JsonSerializable(typeof(PingRequest))]
[JsonSerializable(typeof(PongAck))]
[JsonSerializable(typeof(ErrorAck))]
[JsonSerializable(typeof(SessionStartedEvent))]
[JsonSerializable(typeof(SessionProgressEvent))]
[JsonSerializable(typeof(SessionCompletedEvent))]
[JsonSerializable(typeof(CaptureRefusedEvent))]
[JsonSerializable(typeof(CaptureDegradedEvent))]
[JsonSerializable(typeof(SafetyUnhookEvent))]
[JsonSerializable(typeof(CaptureErrorEvent))]
public sealed partial class IpcJsonContext : JsonSerializerContext
{
}
