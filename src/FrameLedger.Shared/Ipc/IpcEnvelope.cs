using System.Text.Json;

namespace FrameLedger.Shared.Ipc;

/// <summary>
/// <c>{ "type": …, "id": …, "payload": … }</c> (<c>07_IPC</c> §C). An ack correlates by <see cref="Id"/>; an event
/// has none. The payload stays a <see cref="JsonElement"/> until the type is known, so a message the receiver does
/// not understand is still a well-formed envelope it can answer <c>Error</c> to rather than drop the connection over.
/// </summary>
public sealed record IpcEnvelope
{
    public required string Type { get; init; }

    /// <summary>Set on a request and echoed on its ack; absent on an event.</summary>
    public string? Id { get; init; }

    public JsonElement? Payload { get; init; }

    /// <summary>An event, not an answer to anything.</summary>
    public bool IsEvent => Id is null;
}
