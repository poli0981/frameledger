using System.Text.Json;

namespace FrameLedger.Shared.Ipc;

/// <summary>Envelope in, envelope out — through <see cref="IpcJsonContext"/> and nothing else.</summary>
public static class IpcCodec
{
    /// <summary>A request (with <paramref name="id"/>) or an event (without) carrying <paramref name="payload"/>.</summary>
    public static byte[] Encode<T>(string type, string? id, T payload)
        where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(type);
        ArgumentNullException.ThrowIfNull(payload);
        JsonElement element = JsonSerializer.SerializeToElement(payload, typeof(T), IpcJsonContext.Default);
        return Encode(new IpcEnvelope { Type = type, Id = id, Payload = element });
    }

    /// <summary>A message with no payload.</summary>
    public static byte[] Encode(string type, string? id)
    {
        ArgumentException.ThrowIfNullOrEmpty(type);
        return Encode(new IpcEnvelope { Type = type, Id = id });
    }

    public static byte[] Encode(IpcEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return JsonSerializer.SerializeToUtf8Bytes(envelope, IpcJsonContext.Default.IpcEnvelope);
    }

    /// <summary>The envelope a frame carries.</summary>
    /// <exception cref="JsonException">Not an envelope: no <c>type</c>, or not JSON at all.</exception>
    public static IpcEnvelope Decode(ReadOnlySpan<byte> utf8Json) =>
        JsonSerializer.Deserialize(utf8Json, IpcJsonContext.Default.IpcEnvelope)
        ?? throw new JsonException("the envelope decoded to null");

    /// <summary>The payload as <typeparamref name="T"/>, or null when the envelope carries none.</summary>
    /// <exception cref="JsonException">The payload is not a <typeparamref name="T"/>.</exception>
    public static T? Payload<T>(IpcEnvelope envelope)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return envelope.Payload is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } element
            ? (T?)element.Deserialize(typeof(T), IpcJsonContext.Default)
            : null;
    }
}
