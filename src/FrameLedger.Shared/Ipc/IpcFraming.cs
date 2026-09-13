using System.Buffers.Binary;

namespace FrameLedger.Shared.Ipc;

/// <summary>
/// <c>07_IPC</c> §C framing: a 4-byte little-endian length followed by that many bytes of UTF-8 JSON, at most
/// <see cref="IpcProtocol.MaxFrameBytes"/>. Header and body go out in ONE write, so on a message-mode pipe one
/// frame is one message; the reader never relies on that and reassembles by length alone.
/// </summary>
public static class IpcFraming
{
    /// <summary>Reads the length prefix; false when the header is short or the length is over the cap.</summary>
    public static bool TryReadLength(ReadOnlySpan<byte> header, out int length)
    {
        length = 0;
        if (header.Length < IpcProtocol.HeaderBytes)
        {
            return false;
        }

        uint declared = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (declared > IpcProtocol.MaxFrameBytes)
        {
            return false;
        }

        length = (int)declared;
        return true;
    }

    /// <summary>Header + body as one buffer.</summary>
    public static byte[] Frame(ReadOnlySpan<byte> body)
    {
        if (body.Length > IpcProtocol.MaxFrameBytes)
        {
            throw new IpcFramingException($"a {body.Length}-byte frame exceeds the {IpcProtocol.MaxFrameBytes}-byte cap (07_IPC §C)");
        }

        byte[] frame = new byte[IpcProtocol.HeaderBytes + body.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, (uint)body.Length);
        body.CopyTo(frame.AsSpan(IpcProtocol.HeaderBytes));
        return frame;
    }

    public static async ValueTask WriteAsync(Stream stream, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] frame = Frame(body.Span);
        await stream.WriteAsync(frame, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>The next frame's body, or null when the stream ended cleanly between frames.</summary>
    /// <exception cref="IpcFramingException">Over the cap, or the stream ended inside a frame.</exception>
    public static async ValueTask<byte[]?> ReadAsync(Stream stream, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] header = new byte[IpcProtocol.HeaderBytes];
        int got = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct).ConfigureAwait(false);
        if (got == 0)
        {
            return null;
        }

        if (got < header.Length)
        {
            throw new IpcFramingException("the stream ended inside a frame header");
        }

        if (!TryReadLength(header, out int length))
        {
            throw new IpcFramingException(
                $"a frame declares {BinaryPrimitives.ReadUInt32LittleEndian(header)} bytes, over the {IpcProtocol.MaxFrameBytes}-byte cap (07_IPC §C)");
        }

        byte[] body = new byte[length];
        try
        {
            await stream.ReadExactlyAsync(body, ct).ConfigureAwait(false);
        }
        catch (EndOfStreamException ex)
        {
            throw new IpcFramingException($"the stream ended inside a {length}-byte frame body", ex);
        }

        return body;
    }
}
