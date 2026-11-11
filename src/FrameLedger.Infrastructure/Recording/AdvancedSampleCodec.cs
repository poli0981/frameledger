// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Buffers.Binary;
using System.Text;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.Telemetry;

namespace FrameLedger.Infrastructure.Recording;

/// <summary>
/// The advanced readings of a batch of telemetry samples as bytes, for the <c>.partial</c> (chunk type
/// <c>SensorsAdvanced</c>, owner decision D101): the names the batch uses, once, then per sample its QPC and its
/// readings as (name index, value).
/// </summary>
/// <remarks>
/// <para>
/// <b>Beside the wide sensor chunk, not instead of it.</b> The samples of a batch are written as they always were
/// (<see cref="SensorSampleCodec"/>), byte for byte; this chunk follows it and carries only what is new, keyed by the
/// QPC each sample already has. A session recorded with the option off never writes one.
/// </para>
/// <para>
/// <b>Each chunk stands alone</b>: its names are in it. A reader needs nothing from an earlier chunk to read it, so a
/// file cut anywhere still reads up to its last whole chunk — the property the whole format is built on.
/// </para>
/// <para>
/// Layout, little-endian: <c>u16 names</c>, then per name <c>u16 bytes</c> and its UTF-8; <c>u32 samples</c>, then per
/// sample <c>i64 qpc</c>, <c>u16 readings</c> and per reading <c>u16 name</c>, <c>f64 value</c>.
/// </para>
/// </remarks>
internal static class AdvancedSampleCodec
{
    /// <summary>A name longer than this is not a series name this project writes; a chunk claiming one is refused.</summary>
    private const int _maxNameBytes = 256;

    private const int _readingBytes = 2 + 8;
    private const int _sampleHeaderBytes = 8 + 2;

    /// <summary>The chunk for the samples of <paramref name="samples"/> that carry advanced readings, or null when none does.</summary>
    public static byte[]? Write(ReadOnlySpan<TelemetrySample> samples)
    {
        var names = new List<string>();
        var indexOf = new Dictionary<string, ushort>(StringComparer.Ordinal);
        int carrying = 0;
        int bytes = 2 + 4;
        foreach (ref readonly TelemetrySample sample in samples)
        {
            if (sample.Advanced is not { Count: > 0 } readings)
            {
                continue;
            }

            carrying++;
            bytes += _sampleHeaderBytes;
            foreach (AdvancedReading reading in readings)
            {
                if (!Writable(reading.Series) || (!indexOf.ContainsKey(reading.Series) && names.Count >= ushort.MaxValue))
                {
                    continue;
                }

                if (indexOf.TryAdd(reading.Series, (ushort)names.Count))
                {
                    names.Add(reading.Series);
                    bytes += 2 + Encoding.UTF8.GetByteCount(reading.Series);
                }

                bytes += _readingBytes;
            }
        }

        return carrying == 0 ? null : Fill(new byte[bytes], samples, names, indexOf, carrying);
    }

    /// <summary>The readings of each sample in a chunk, by the sample's QPC; false when the chunk is not well formed.</summary>
    public static bool TryRead(ReadOnlySpan<byte> payload, List<(long Qpc, IReadOnlyList<AdvancedReading> Readings)> into)
    {
        int at = 0;
        if (!TryReadNames(payload, ref at, out string[] names) || payload.Length - at < 4)
        {
            return false;
        }

        uint samples = BinaryPrimitives.ReadUInt32LittleEndian(payload[at..]);
        at += 4;
        var read = new List<(long, IReadOnlyList<AdvancedReading>)>();
        for (uint s = 0; s < samples; s++)
        {
            if (payload.Length - at < _sampleHeaderBytes)
            {
                return false;
            }

            long qpc = BinaryPrimitives.ReadInt64LittleEndian(payload[at..]);
            int count = BinaryPrimitives.ReadUInt16LittleEndian(payload[(at + 8)..]);
            at += _sampleHeaderBytes;
            if (payload.Length - at < count * _readingBytes)
            {
                return false;
            }

            var readings = new AdvancedReading[count];
            for (int r = 0; r < count; r++)
            {
                int index = BinaryPrimitives.ReadUInt16LittleEndian(payload[at..]);
                if (index >= names.Length)
                {
                    return false;
                }

                readings[r] = new AdvancedReading(names[index], BinaryPrimitives.ReadDoubleLittleEndian(payload[(at + 2)..]));
                at += _readingBytes;
            }

            read.Add((qpc, readings));
        }

        if (at != payload.Length)
        {
            return false;
        }

        into.AddRange(read);
        return true;
    }

    private static bool Writable(string? series) => series is not null && Encoding.UTF8.GetByteCount(series) is > 0 and <= _maxNameBytes;

    private static byte[] Fill(byte[] payload, ReadOnlySpan<TelemetrySample> samples, List<string> names, Dictionary<string, ushort> indexOf, int carrying)
    {
        int at = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(at), (ushort)names.Count);
        at += 2;
        foreach (string name in names)
        {
            int length = Encoding.UTF8.GetBytes(name, payload.AsSpan(at + 2));
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(at), (ushort)length);
            at += 2 + length;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at), (uint)carrying);
        at += 4;
        foreach (ref readonly TelemetrySample sample in samples)
        {
            if (sample.Advanced is not { Count: > 0 } readings)
            {
                continue;
            }

            BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(at), sample.QpcTicks);
            int countAt = at + 8;
            at += _sampleHeaderBytes;
            ushort count = 0;
            foreach (AdvancedReading reading in readings)
            {
                if (reading.Series is null || !indexOf.TryGetValue(reading.Series, out ushort index))
                {
                    continue;
                }

                BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(at), index);
                BinaryPrimitives.WriteDoubleLittleEndian(payload.AsSpan(at + 2), reading.Value);
                at += _readingBytes;
                count++;
            }

            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(countAt), count);
        }

        return at == payload.Length ? payload : payload[..at];
    }

    private static bool TryReadNames(ReadOnlySpan<byte> payload, ref int at, out string[] names)
    {
        names = [];
        if (payload.Length - at < 2)
        {
            return false;
        }

        int count = BinaryPrimitives.ReadUInt16LittleEndian(payload[at..]);
        at += 2;
        string[] read = new string[count];
        for (int i = 0; i < count; i++)
        {
            if (payload.Length - at < 2)
            {
                return false;
            }

            int length = BinaryPrimitives.ReadUInt16LittleEndian(payload[at..]);
            at += 2;
            if (length is 0 or > _maxNameBytes || payload.Length - at < length)
            {
                return false;
            }

            read[i] = Encoding.UTF8.GetString(payload.Slice(at, length));
            at += length;
        }

        names = read;
        return true;
    }
}
