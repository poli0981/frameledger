// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.Input;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Telemetry;
using FrameLedger.Shared;

namespace FrameLedger.Infrastructure.Recording;

/// <summary>
/// The <c>.partial</c> format (<c>06_DATA_MODEL</c> §The <c>.partial</c> file): append-only chunks,
/// <c>u32 type | u32 length | payload | u32 crc32(type ‖ length ‖ payload)</c>, little-endian. The reader
/// stops at the first chunk that is short or fails its check, so a kill at any byte offset loses at most
/// the chunk being written and everything before it stands (<c>PartialSessionFileTests</c> kills at every byte).
/// </summary>
public static class PartialSessionFile
{
    private const int _chunkOverhead = 12;
    private const int _recordBytes = 64;

    // A header is a few hundred bytes of JSON; a length past this is a damaged file, not a header to allocate for.
    private const int _maxHeaderBytes = 1024 * 1024;

    /// <summary>Opens a new file and writes the header chunk. Never overwrites: a guid is one session.</summary>
    public static IPartialSessionWriter Create(string path, PartialHeader header)
    {
        ArgumentNullException.ThrowIfNull(header);
        Writer? writer = null;
        try
        {
            writer = new Writer(path);
            writer.Append(PartialChunkType.Header, JsonSerializer.SerializeToUtf8Bytes(header, PartialJsonContext.Default.PartialHeader));
            Writer created = writer;
            writer = null;    // the caller owns it now
            return created;
        }
        finally
        {
            writer?.Dispose();
        }
    }

    /// <summary>The valid prefix of <paramref name="path"/>; null when the header itself is missing or unreadable.</summary>
    public static PartialSession? Read(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return Parse(bytes);
    }

    /// <summary>
    /// The header alone (2026-09-23), read beside the running session's writer — which holds the file for writing and
    /// shares it for reading only, so this opens it sharing read, write and delete. Null when the file is gone, cannot
    /// be opened, or its first chunk is not a whole header.
    /// </summary>
    public static PartialHeader? ReadHeader(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Span<byte> head = stackalloc byte[8];
            if (stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false) < head.Length)
            {
                return null;
            }

            int length = (int)BinaryPrimitives.ReadUInt32LittleEndian(head[4..]);
            if (length < 0 || length > _maxHeaderBytes)
            {
                return null;
            }

            byte[] chunk = new byte[_chunkOverhead + length];
            head.CopyTo(chunk);
            if (stream.ReadAtLeast(chunk.AsSpan(head.Length), chunk.Length - head.Length, throwOnEndOfStream: false) < chunk.Length - head.Length)
            {
                return null;
            }

            return Parse(chunk)?.Header;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The valid prefix of <paramref name="bytes"/>; null when the header itself is missing or unreadable.</summary>
    public static PartialSession? Parse(ReadOnlySpan<byte> bytes)
    {
        var acc = new Accumulator();
        int at = 0;
        bool truncated = false;
        while (at < bytes.Length)
        {
            if (!TryReadChunk(bytes, ref at, out PartialChunkType type, out ReadOnlySpan<byte> payload) || !acc.Apply(type, payload))
            {
                truncated = true;
                break;
            }
        }

        return acc.Header is null ? null : acc.ToSession(truncated);
    }

    private static bool TryReadChunk(ReadOnlySpan<byte> bytes, ref int at, out PartialChunkType type, out ReadOnlySpan<byte> payload)
    {
        type = PartialChunkType.None;
        payload = default;
        if (bytes.Length - at < _chunkOverhead)
        {
            return false;
        }

        uint rawType = BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
        int length = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 4)..]);
        if (length < 0 || bytes.Length - at - _chunkOverhead < length)
        {
            return false;
        }

        ReadOnlySpan<byte> checkedPart = bytes.Slice(at, 8 + length);
        uint stored = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 8 + length)..]);
        if (Crc32.Of(checkedPart) != stored)
        {
            return false;
        }

        type = (PartialChunkType)rawType;
        payload = bytes.Slice(at + 8, length);
        at += _chunkOverhead + length;
        return true;
    }

    private sealed class Accumulator
    {
        public PartialHeader? Header { get; private set; }

        private readonly List<FlFrameRecord> _records = [];
        private readonly List<int> _gaps = [];
        private readonly List<TelemetrySample> _sensors = [];
        private readonly List<long> _touches = [];
        private readonly List<PartialNote> _notes = [];
        private PartialTick? _tick;
        private string? _advancedContext;

        public bool Apply(PartialChunkType type, ReadOnlySpan<byte> payload)
        {
            if (Header is null)
            {
                return type == PartialChunkType.Header && TryHeader(payload);
            }

            return type switch
            {
                PartialChunkType.Records => Records(payload),
                PartialChunkType.Gaps => Int32s(payload, _gaps),
                PartialChunkType.Sensors => Sensors(payload, wide: false),
                PartialChunkType.SensorsWide => Sensors(payload, wide: true),
                PartialChunkType.Tick => Tick(payload),
                PartialChunkType.Note => Note(payload),
                PartialChunkType.Touches => Int64s(payload, _touches),
                PartialChunkType.SensorsAdvanced => AdvancedSensors(payload),
                PartialChunkType.AdvancedContext => AdvancedContext(payload),
                PartialChunkType.SensorsInput => InputSensors(payload),

                // A type this build does not know, in a chunk whose length and checksum are right (TryReadChunk has
                // verified both): a newer build wrote it, and it is passed over (owner decision D106, 2026-11-11).
                // Until then it ended the read, so a build one release older lost everything after the first new
                // chunk - where a chunk it does know and cannot read still ends it, because that is a torn or a
                // wrong file, not a newer one.
                _ => true,
            };
        }

        public PartialSession ToSession(bool truncated) => new()
        {
            Header = Header!,
            Records = _records,
            GapBefore = _gaps,
            Sensors = _sensors,
            AdvancedContextJson = _advancedContext,
            TouchQpc = _touches,
            Notes = _notes,
            LastTick = _tick,
            Truncated = truncated,
        };

        private bool TryHeader(ReadOnlySpan<byte> payload)
        {
            try
            {
                PartialHeader? h = JsonSerializer.Deserialize(payload, PartialJsonContext.Default.PartialHeader);
                if (h is null || h.FormatVersion != PartialHeader.CurrentFormatVersion)
                {
                    return false;
                }

                Header = h;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private bool Records(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < 8 || (payload.Length - 8) % _recordBytes != 0)
            {
                return false;
            }

            long first = BinaryPrimitives.ReadInt64LittleEndian(payload);
            if (first != _records.Count)
            {
                // A chunk that does not continue the sequence is a writer bug, not a torn tail; refuse it.
                return false;
            }

            _records.AddRange(MemoryMarshal.Cast<byte, FlFrameRecord>(payload[8..]).ToArray());
            return true;
        }

        private static bool Int32s(ReadOnlySpan<byte> payload, List<int> into)
        {
            if (payload.Length % 4 != 0)
            {
                return false;
            }

            for (int i = 0; i < payload.Length; i += 4)
            {
                into.Add(BinaryPrimitives.ReadInt32LittleEndian(payload[i..]));
            }

            return true;
        }

        private static bool Int64s(ReadOnlySpan<byte> payload, List<long> into)
        {
            if (payload.Length % 8 != 0)
            {
                return false;
            }

            for (int i = 0; i < payload.Length; i += 8)
            {
                into.Add(BinaryPrimitives.ReadInt64LittleEndian(payload[i..]));
            }

            return true;
        }

        private bool Sensors(ReadOnlySpan<byte> payload, bool wide)
        {
            int at = 0;
            while (at < payload.Length)
            {
                int used = SensorSampleCodec.Read(payload[at..], wide, out TelemetrySample sample);
                if (used == 0)
                {
                    return false;
                }

                _sensors.Add(sample);
                at += used;
            }

            return true;
        }

        /// <summary>
        /// The advanced readings of samples already read: each is attached to the sample with its QPC — the last one
        /// that has none yet, since the chunk follows the sensor chunk of the same flush. A reading whose sample is not
        /// there is left out; it has no tick to belong to.
        /// </summary>
        private bool AdvancedSensors(ReadOnlySpan<byte> payload)
        {
            var read = new List<(long Qpc, IReadOnlyList<AdvancedReading> Readings)>();
            if (!AdvancedSampleCodec.TryRead(payload, read))
            {
                return false;
            }

            foreach ((long qpc, IReadOnlyList<AdvancedReading> readings) in read)
            {
                for (int i = _sensors.Count - 1; i >= 0; i--)
                {
                    if (_sensors[i].QpcTicks == qpc && _sensors[i].Advanced is null)
                    {
                        _sensors[i] = _sensors[i] with { Advanced = readings };
                        break;
                    }
                }
            }

            return true;
        }

        /// <summary>The input readings of samples already read, attached as <see cref="AdvancedSensors"/> attaches its own: by QPC, to the last sample that has none yet.</summary>
        private bool InputSensors(ReadOnlySpan<byte> payload)
        {
            var read = new List<(long Qpc, InputReading Reading)>();
            if (!InputSampleCodec.TryRead(payload, read))
            {
                return false;
            }

            foreach ((long qpc, InputReading reading) in read)
            {
                for (int i = _sensors.Count - 1; i >= 0; i--)
                {
                    if (_sensors[i].QpcTicks == qpc && _sensors[i].Input is null)
                    {
                        _sensors[i] = _sensors[i] with { Input = reading };
                        break;
                    }
                }
            }

            return true;
        }

        private bool AdvancedContext(ReadOnlySpan<byte> payload)
        {
            _advancedContext = Encoding.UTF8.GetString(payload);
            return true;
        }

        private bool Tick(ReadOnlySpan<byte> payload)
        {
            int stateBytes = Marshal.SizeOf<FlWriterState>();
            if (payload.Length != 8 * 5 + 4 + stateBytes)
            {
                return false;
            }

            _tick = new PartialTick(
                BinaryPrimitives.ReadInt64LittleEndian(payload),
                BinaryPrimitives.ReadInt64LittleEndian(payload[8..]),
                BinaryPrimitives.ReadInt64LittleEndian(payload[16..]),
                BinaryPrimitives.ReadInt64LittleEndian(payload[24..]),
                BinaryPrimitives.ReadUInt32LittleEndian(payload[32..]),
                BinaryPrimitives.ReadInt64LittleEndian(payload[36..]),
                MemoryMarshal.Read<FlWriterState>(payload[44..]));
            return true;
        }

        private bool Note(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < 8)
            {
                return false;
            }

            long at = BinaryPrimitives.ReadInt64LittleEndian(payload);
            _notes.Add(new PartialNote(DateTimeOffset.FromUnixTimeMilliseconds(at), Encoding.UTF8.GetString(payload[8..])));
            return true;
        }
    }

    private sealed class Writer : IPartialSessionWriter
    {
        private readonly FileStream _stream;
        private readonly byte[] _frame = new byte[8];

        /// <summary>Opens the file itself, so the stream is born owned (CA2000 has no hand-off to reason about).</summary>
        public Writer(string path) => _stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);

        public void AppendRecords(long firstOrdinal, ReadOnlySpan<FlFrameRecord> records)
        {
            if (records.IsEmpty)
            {
                return;
            }

            byte[] payload = new byte[8 + records.Length * _recordBytes];
            BinaryPrimitives.WriteInt64LittleEndian(payload, firstOrdinal);
            MemoryMarshal.AsBytes(records).CopyTo(payload.AsSpan(8));
            Append(PartialChunkType.Records, payload);
        }

        public void AppendGaps(ReadOnlySpan<int> gapBefore)
        {
            if (gapBefore.IsEmpty)
            {
                return;
            }

            byte[] payload = new byte[gapBefore.Length * 4];
            for (int i = 0; i < gapBefore.Length; i++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(i * 4), gapBefore[i]);
            }

            Append(PartialChunkType.Gaps, payload);
        }

        public void AppendSensors(ReadOnlySpan<TelemetrySample> samples)
        {
            if (samples.IsEmpty)
            {
                return;
            }

            int size = 0;
            foreach (ref readonly TelemetrySample s in samples)
            {
                size += SensorSampleCodec.SizeOf(in s);
            }

            byte[] payload = new byte[size];
            int at = 0;
            foreach (ref readonly TelemetrySample s in samples)
            {
                at += SensorSampleCodec.Write(payload.AsSpan(at), in s);
            }

            Append(PartialChunkType.SensorsWide, payload);

            // The advanced readings of the same samples, in a chunk of their own after it - and only when there are
            // any, so with the option off the file is byte for byte what it was (D101).
            if (AdvancedSampleCodec.Write(samples) is byte[] advanced)
            {
                Append(PartialChunkType.SensorsAdvanced, advanced);
            }

            // And the input readings, the same way and for the same reason (D155-D163).
            if (InputSampleCodec.Write(samples) is byte[] input)
            {
                Append(PartialChunkType.SensorsInput, input);
            }
        }

        public void AppendAdvancedContext(string json)
        {
            ArgumentNullException.ThrowIfNull(json);
            Append(PartialChunkType.AdvancedContext, Encoding.UTF8.GetBytes(json));
        }

        public void AppendTick(PartialTick tick)
        {
            FlWriterState state = tick.WriterState;
            byte[] payload = new byte[8 * 5 + 4 + Marshal.SizeOf<FlWriterState>()];
            BinaryPrimitives.WriteInt64LittleEndian(payload, tick.DrainTicks);
            BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(8), tick.ForegroundTicks);
            BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(16), tick.TotalDropped);
            BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(24), tick.TotalGaps);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(32), tick.GuardTicksPublished);
            BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(36), tick.WrittenAtUnixMs);
            MemoryMarshal.Write(payload.AsSpan(44), in state);
            Append(PartialChunkType.Tick, payload);
        }

        public void AppendTouches(ReadOnlySpan<long> touchQpc)
        {
            if (touchQpc.IsEmpty)
            {
                return;
            }

            byte[] payload = new byte[touchQpc.Length * 8];
            for (int i = 0; i < touchQpc.Length; i++)
            {
                BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(i * 8), touchQpc[i]);
            }

            Append(PartialChunkType.Touches, payload);
        }

        public void AppendNote(DateTimeOffset at, string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            byte[] utf8 = Encoding.UTF8.GetBytes(text);
            byte[] payload = new byte[8 + utf8.Length];
            BinaryPrimitives.WriteInt64LittleEndian(payload, at.ToUnixTimeMilliseconds());
            utf8.CopyTo(payload, 8);
            Append(PartialChunkType.Note, payload);
        }

        /// <summary>One chunk, then a flush to the OS: the threat is this process dying, and a flushed chunk survives that.</summary>
        public void Append(PartialChunkType type, ReadOnlySpan<byte> payload)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(_frame, (uint)type);
            BinaryPrimitives.WriteUInt32LittleEndian(_frame.AsSpan(4), (uint)payload.Length);
            uint crc = Crc32.Append(0xFFFFFFFFu, _frame);
            crc = Crc32.Append(crc, payload) ^ 0xFFFFFFFFu;
            Span<byte> trailer = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(trailer, crc);

            _stream.Write(_frame);
            _stream.Write(payload);
            _stream.Write(trailer);
            _stream.Flush();
        }

        public void Dispose() => _stream.Dispose();
    }
}
