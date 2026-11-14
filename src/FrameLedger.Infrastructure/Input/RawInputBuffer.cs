// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Buffers.Binary;
using FrameLedger.Application.Input;

namespace FrameLedger.Infrastructure.Input;

/// <summary>
/// The bytes <c>GetRawInputBuffer</c> and <c>GetRawInputData</c> fill, walked without a pointer: a run of
/// <c>RAWINPUT</c> blocks, each a header and - for a HID device - a count of reports of one size
/// (<c>21_INPUT_DEVICES</c> §The reader).
/// </summary>
/// <remarks>
/// <para>
/// Layout on 64-bit Windows, which is all this project builds for: <c>RAWINPUTHEADER</c> is 24 bytes - <c>u32 type</c>,
/// <c>u32 size</c> of the whole block, the device's handle, a parameter - and a <c>RAWHID</c> follows it: <c>u32</c>
/// the size of one report, <c>u32</c> how many, then the reports. The next block starts at the next multiple of eight
/// (<c>NEXTRAWINPUTBLOCK</c>).
/// </para>
/// <para>
/// <b>It trusts no length it reads.</b> A block that claims more than the buffer holds, or less than its own header,
/// ends the walk: what was handed over before it stands, and nothing is read past the buffer. The bytes come from a
/// device by way of Windows, into an Agent that may run as administrator.
/// </para>
/// </remarks>
public static class RawInputBuffer
{
    /// <summary><c>sizeof(RAWINPUTHEADER)</c> on 64-bit Windows.</summary>
    public const int HeaderBytes = 24;

    /// <summary><c>RIM_TYPEHID</c>: the block is a HID device's, the only kind the reader registers for.</summary>
    public const uint TypeHid = 2;

    private const int _hidHeaderBytes = 8;

    /// <summary>Hands each report of each HID block to <paramref name="sink"/>; returns how many blocks were walked.</summary>
    /// <param name="buffer">The bytes Windows filled.</param>
    /// <param name="blocks">How many blocks Windows said it wrote.</param>
    /// <param name="sink">Told each report with its device's handle.</param>
    public static int Walk(ReadOnlySpan<byte> buffer, int blocks, IInputBackendSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        int at = 0;
        int walked = 0;
        for (; walked < blocks; walked++)
        {
            if (buffer.Length - at < HeaderBytes)
            {
                break;
            }

            uint type = BinaryPrimitives.ReadUInt32LittleEndian(buffer[at..]);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(buffer[(at + 4)..]);
            if (size < HeaderBytes || size > buffer.Length - at)
            {
                break;
            }

            if (type == TypeHid)
            {
                Reports(buffer.Slice(at, (int)size), sink);
            }

            // Each block starts on a multiple of eight. The last one may end the buffer without the padding.
            at += (int)((size + 7) & ~7u);
            if (at > buffer.Length)
            {
                walked++;
                break;
            }
        }

        return walked;
    }

    private static void Reports(ReadOnlySpan<byte> block, IInputBackendSink sink)
    {
        if (block.Length < HeaderBytes + _hidHeaderBytes)
        {
            return;
        }

        nint device = (nint)BinaryPrimitives.ReadInt64LittleEndian(block[8..]);
        uint reportBytes = BinaryPrimitives.ReadUInt32LittleEndian(block[HeaderBytes..]);
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(block[(HeaderBytes + 4)..]);
        ReadOnlySpan<byte> reports = block[(HeaderBytes + _hidHeaderBytes)..];
        if (reportBytes == 0 || count == 0 || (ulong)reportBytes * count > (ulong)reports.Length)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            sink.Report(device, reports.Slice(i * (int)reportBytes, (int)reportBytes));
        }
    }
}
