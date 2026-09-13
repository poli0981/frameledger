using System.Buffers.Binary;
using System.Text;
using FluentAssertions;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Infrastructure.Tests.Ipc;

/// <summary>
/// <c>07_IPC</c> §C framing: 4-byte LE length + body, ≤ 1 MB. Every refusal is driven — a cap that is never
/// hit is a comment.
/// </summary>
public sealed class IpcFramingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ABodyRoundTripsThroughOneFrame()
    {
        byte[] body = Encoding.UTF8.GetBytes("{\"type\":\"Ping\",\"id\":\"1\"}");
        using var stream = new MemoryStream();
        await IpcFraming.WriteAsync(stream, body, Ct).ConfigureAwait(true);

        stream.Length.Should().Be(IpcProtocol.HeaderBytes + body.Length);
        BinaryPrimitives.ReadUInt32LittleEndian(stream.GetBuffer()).Should().Be((uint)body.Length, "little-endian, unsigned");

        stream.Position = 0;
        byte[]? read = await IpcFraming.ReadAsync(stream, Ct).ConfigureAwait(true);
        read.Should().Equal(body);
        (await IpcFraming.ReadAsync(stream, Ct).ConfigureAwait(true)).Should().BeNull("a clean end between frames is not an error");
    }

    [Fact]
    public async Task AnEmptyBodyIsAValidFrame()
    {
        using var stream = new MemoryStream();
        await IpcFraming.WriteAsync(stream, ReadOnlyMemory<byte>.Empty, Ct).ConfigureAwait(true);
        stream.Position = 0;
        (await IpcFraming.ReadAsync(stream, Ct).ConfigureAwait(true)).Should().BeEmpty();
    }

    [Fact]
    public void ABodyOverTheCapIsRefusedBeforeItIsWritten()
    {
        byte[] over = new byte[IpcProtocol.MaxFrameBytes + 1];
        Action frame = () => IpcFraming.Frame(over);
        frame.Should().Throw<IpcFramingException>().WithMessage("*cap*");

        IpcFraming.Frame(new byte[IpcProtocol.MaxFrameBytes]).Should().HaveCount(IpcProtocol.HeaderBytes + IpcProtocol.MaxFrameBytes, "exactly the cap is allowed");
    }

    [Fact]
    public async Task AHeaderClaimingMoreThanTheCapIsRefusedWithoutReadingTheBody()
    {
        byte[] header = new byte[IpcProtocol.HeaderBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)IpcProtocol.MaxFrameBytes + 1);
        using var stream = new MemoryStream(header);

        Func<Task> read = async () => await IpcFraming.ReadAsync(stream, Ct).ConfigureAwait(true);
        await read.Should().ThrowAsync<IpcFramingException>().WithMessage("*cap*").ConfigureAwait(true);
        IpcFraming.TryReadLength(header, out _).Should().BeFalse();
    }

    [Fact]
    public async Task AStreamThatEndsInsideABodyIsAFramingErrorNotAnEmptyFrame()
    {
        byte[] frame = IpcFraming.Frame(Encoding.UTF8.GetBytes("{\"type\":\"Ping\"}"));
        using var stream = new MemoryStream(frame, 0, frame.Length - 3);

        Func<Task> read = async () => await IpcFraming.ReadAsync(stream, Ct).ConfigureAwait(true);
        await read.Should().ThrowAsync<IpcFramingException>().WithMessage("*inside*").ConfigureAwait(true);
    }

    [Fact]
    public async Task AStreamThatEndsInsideAHeaderIsAFramingError()
    {
        using var stream = new MemoryStream([0x05, 0x00]);
        Func<Task> read = async () => await IpcFraming.ReadAsync(stream, Ct).ConfigureAwait(true);
        await read.Should().ThrowAsync<IpcFramingException>().WithMessage("*header*").ConfigureAwait(true);
    }

    [Fact]
    public void AShortHeaderReadsAsNoLength()
    {
        IpcFraming.TryReadLength([0x01, 0x00, 0x00], out int length).Should().BeFalse();
        length.Should().Be(0);
        IpcFraming.TryReadLength([0x10, 0x00, 0x00, 0x00, 0xFF], out length).Should().BeTrue("extra bytes after the header are the body");
        length.Should().Be(16);
    }
}
