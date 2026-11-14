// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Buffers.Binary;
using FluentAssertions;
using FrameLedger.Application.Input;
using FrameLedger.Infrastructure.Input;

namespace FrameLedger.Infrastructure.Tests.Input;

/// <summary>
/// The bytes <c>GetRawInputBuffer</c> fills, walked (<c>21_INPUT_DEVICES</c> §The reader): each report of each HID block
/// reaches the sink with its device, and a block that claims more than the buffer holds ends the walk without a byte
/// read past it. The buffers are built here to the documented layout - <b>none was filled by Windows</b>.
/// </summary>
public sealed class RawInputBufferTests
{
    /// <summary>One block: the header, then for a HID device the size and count of its reports and the reports themselves.</summary>
    private static byte[] Block(nint device, int reportBytes, params byte[][] reports)
    {
        byte[] block = new byte[RawInputBuffer.HeaderBytes + 8 + reports.Sum(static r => r.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(block, RawInputBuffer.TypeHid);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(4), (uint)block.Length);
        BinaryPrimitives.WriteInt64LittleEndian(block.AsSpan(8), device);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(RawInputBuffer.HeaderBytes), (uint)reportBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(RawInputBuffer.HeaderBytes + 4), (uint)reports.Length);
        int at = RawInputBuffer.HeaderBytes + 8;
        foreach (byte[] report in reports)
        {
            report.CopyTo(block, at);
            at += report.Length;
        }

        return block;
    }

    /// <summary>Blocks one after another, each starting on a multiple of eight as Windows lays them out.</summary>
    private static byte[] Buffer(params byte[][] blocks)
    {
        var bytes = new List<byte>();
        foreach (byte[] block in blocks)
        {
            bytes.AddRange(block);
            while (bytes.Count % 8 != 0)
            {
                bytes.Add(0xEE);
            }
        }

        return [.. bytes];
    }

    [Fact]
    public void EachReportOfEachBlockReachesTheSinkWithItsDevice()
    {
        var sink = new Recorder();
        byte[] buffer = Buffer(
            Block(0x10, 3, [1, 2, 3], [4, 5, 6]),
            Block(0x20, 5, [9, 9, 9, 9, 9]));

        RawInputBuffer.Walk(buffer, 2, sink).Should().Be(2);

        sink.Reports.Should().HaveCount(3);
        sink.Reports[0].Should().Be(((nint)0x10, "010203"));
        sink.Reports[1].Should().Be(((nint)0x10, "040506"));
        sink.Reports[2].Should().Be(((nint)0x20, "0909090909"));
    }

    [Fact]
    public void OnlyAsManyBlocksAsWindowsSaidItWroteAreWalked()
    {
        var sink = new Recorder();
        byte[] buffer = Buffer(Block(0x10, 1, [1]), Block(0x20, 1, [2]));

        RawInputBuffer.Walk(buffer, 1, sink).Should().Be(1);

        sink.Reports.Should().ContainSingle().Which.Device.Should().Be(0x10);
        RawInputBuffer.Walk(buffer, 0, sink).Should().Be(0);
        RawInputBuffer.Walk([], 3, sink).Should().Be(0);
    }

    [Fact]
    public void ABlockThatIsNotAHidDevicesIsPassedOver()
    {
        var sink = new Recorder();
        byte[] keyboard = new byte[RawInputBuffer.HeaderBytes + 16];
        BinaryPrimitives.WriteUInt32LittleEndian(keyboard, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(keyboard.AsSpan(4), (uint)keyboard.Length);

        RawInputBuffer.Walk(Buffer(keyboard, Block(0x20, 1, [7])), 2, sink).Should().Be(2);

        sink.Reports.Should().ContainSingle("the reader registers for controllers only; a block of another kind is never read").Which.Device.Should().Be(0x20);
    }

    [Fact]
    public void TheLastBlockMayEndTheBufferWithoutItsPadding()
    {
        var sink = new Recorder();
        byte[] block = Block(0x10, 3, [1, 2, 3]);
        block.Length.Should().Be(35, "not a multiple of eight: the walk must not want the five bytes after it");

        RawInputBuffer.Walk(block, 1, sink).Should().Be(1);

        sink.Reports.Should().ContainSingle();
    }

    /// <summary>The bytes come from a device by way of Windows: no length in them is trusted.</summary>
    [Fact]
    public void ABlockThatClaimsMoreThanTheBufferHoldsEndsTheWalkAndWhatCameBeforeStands()
    {
        var sink = new Recorder();
        byte[] good = Block(0x10, 2, [1, 2]);
        byte[] lying = Block(0x20, 2, [3, 4]);
        BinaryPrimitives.WriteUInt32LittleEndian(lying.AsSpan(4), 4096);

        RawInputBuffer.Walk(Buffer(good, lying), 2, sink).Should().Be(1);

        sink.Reports.Should().ContainSingle().Which.Device.Should().Be(0x10);
    }

    [Fact]
    public void ABlockSmallerThanItsOwnHeaderEndsTheWalk()
    {
        var sink = new Recorder();
        byte[] tiny = Block(0x10, 1, [1]);
        BinaryPrimitives.WriteUInt32LittleEndian(tiny.AsSpan(4), 8);

        RawInputBuffer.Walk(Buffer(tiny, Block(0x20, 1, [2])), 2, sink).Should().Be(0);

        sink.Reports.Should().BeEmpty();
        RawInputBuffer.Walk(new byte[10], 1, sink).Should().Be(0, "not even a header");
    }

    [Theory]
    [InlineData(0u, 4u)] // reports of no size
    [InlineData(4u, 0u)] // no reports
    [InlineData(4u, 3u)] // three reports of four bytes where eight bytes follow
    [InlineData(0xFFFFFFFFu, 0xFFFFFFFFu)] // a product that would not fit in 32 bits
    public void ReportsThatDoNotFitTheirBlockAreNotHandedOver(uint reportBytes, uint count)
    {
        var sink = new Recorder();
        byte[] block = Block(0x10, 4, [1, 2, 3, 4], [5, 6, 7, 8]);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(RawInputBuffer.HeaderBytes), reportBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(RawInputBuffer.HeaderBytes + 4), count);

        RawInputBuffer.Walk(Buffer(block, Block(0x20, 1, [9])), 2, sink).Should().Be(2, "the block is walked over by its own size, which is sound");

        sink.Reports.Should().ContainSingle("only the block after it").Which.Device.Should().Be(0x20);
    }

    [Fact]
    public void AHidBlockWithNoRoomForItsCountsHasNoReports()
    {
        var sink = new Recorder();
        byte[] header = new byte[RawInputBuffer.HeaderBytes + 4];
        BinaryPrimitives.WriteUInt32LittleEndian(header, RawInputBuffer.TypeHid);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), (uint)header.Length);

        RawInputBuffer.Walk(header, 1, sink).Should().Be(1);

        sink.Reports.Should().BeEmpty();
    }

    private sealed class Recorder : IInputBackendSink
    {
        public List<(nint Device, string Hex)> Reports { get; } = [];

        public void Arrived(nint device, RawController controller)
        {
        }

        public void Removed(nint device)
        {
        }

        public void Report(nint device, ReadOnlySpan<byte> report) => Reports.Add((device, Convert.ToHexString(report)));
    }
}
