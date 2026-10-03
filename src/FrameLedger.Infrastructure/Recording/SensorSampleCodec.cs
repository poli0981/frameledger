// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Buffers.Binary;
using FrameLedger.Application.Telemetry;

namespace FrameLedger.Infrastructure.Recording;

/// <summary>
/// One <see cref="TelemetrySample"/> as bytes, for the <c>.partial</c>: QPC, wall clock, layer, then a
/// presence mask and only the fields that carry a value — null stays null across the round trip, which
/// is the whole point (N/A is never 0).
/// </summary>
/// <remarks>
/// Two forms. The NARROW one (chunk type <c>Sensors</c>, written until beta.11) has a 16-bit mask over 15 fields. The WIDE
/// one (chunk type <c>SensorsWide</c>, written from beta.12) has a 32-bit mask over the same 15 fields plus the game
/// process's memory (D43): five MiB figures, the process count and the sources. Both are read; only the wide one is
/// written.
/// </remarks>
internal static class SensorSampleCodec
{
    // 12 GPU fields + 3 system fields (2026-09-21) in the narrow 16-bit mask. A .partial written before that date has bits
    // 12..14 clear and reads back with an empty SystemReading, which is what it recorded.
    private const int _narrowFields = 15;

    // + 5 game-memory figures, the process count and the sources (beta.12, D43) in the wide 32-bit mask.
    private const int _wideFields = 22;
    private const int _narrowFixedBytes = 8 + 8 + 1 + 2;
    private const int _wideFixedBytes = 8 + 8 + 1 + 4;

    public static int SizeOf(in TelemetrySample sample) => _wideFixedBytes + 8 * Fields(in sample).Count(static f => f is not null);

    /// <summary>Writes the wide form; returns the bytes written.</summary>
    public static int Write(Span<byte> into, in TelemetrySample sample)
    {
        GpuSample s = sample.Sample;
        BinaryPrimitives.WriteInt64LittleEndian(into, sample.QpcTicks);
        BinaryPrimitives.WriteInt64LittleEndian(into[8..], s.TakenAt.ToUnixTimeMilliseconds());
        into[16] = (byte)s.Layer;
        uint mask = 0;
        int at = _wideFixedBytes;
        double?[] fields = Fields(in sample);
        for (int i = 0; i < _wideFields; i++)
        {
            if (fields[i] is { } v)
            {
                mask |= 1u << i;
                BinaryPrimitives.WriteDoubleLittleEndian(into[at..], v);
                at += 8;
            }
        }

        BinaryPrimitives.WriteUInt32LittleEndian(into[17..], mask);
        return at;
    }

    /// <summary>Reads one sample in either form; returns the bytes consumed, or 0 when the span is too short.</summary>
    public static int Read(ReadOnlySpan<byte> from, bool wide, out TelemetrySample sample)
    {
        sample = default;
        int fixedBytes = wide ? _wideFixedBytes : _narrowFixedBytes;
        if (from.Length < fixedBytes)
        {
            return 0;
        }

        long qpc = BinaryPrimitives.ReadInt64LittleEndian(from);
        long takenAt = BinaryPrimitives.ReadInt64LittleEndian(from[8..]);
        var layer = (TelemetryLayer)from[16];
        uint mask = wide ? BinaryPrimitives.ReadUInt32LittleEndian(from[17..]) : BinaryPrimitives.ReadUInt16LittleEndian(from[17..]);
        int count = wide ? _wideFields : _narrowFields;
        int present = System.Numerics.BitOperations.PopCount(mask);
        if (from.Length < fixedBytes + 8 * present)
        {
            return 0;
        }

        var fields = new double?[_wideFields];
        int at = fixedBytes;
        for (int i = 0; i < count; i++)
        {
            if ((mask & (1u << i)) != 0)
            {
                fields[i] = BinaryPrimitives.ReadDoubleLittleEndian(from[at..]);
                at += 8;
            }
        }

        sample = new TelemetrySample(qpc, new GpuSample
        {
            TakenAt = DateTimeOffset.FromUnixTimeMilliseconds(takenAt),
            Layer = layer,
            TempCoreC = fields[0],
            TempHotspotC = fields[1],
            TempMemoryC = fields[2],
            LoadPct = fields[3],
            VramAdapterMb = fields[4],
            CoreClockMhz = fields[5],
            MemClockMhz = fields[6],
            PowerW = fields[7],
            FanRpm = fields[8],
            ThrottleReasons = fields[9] is { } t ? (uint)t : null,
            PcieGen = fields[10] is { } g ? (int)g : null,
            PcieWidth = fields[11] is { } w ? (int)w : null,
        }, new SystemReading(fields[12], fields[13], fields[14]), new ProcessReading(
            VramDedicatedMb: fields[15],
            VramSharedMb: fields[16],
            RamPrivateMb: fields[17],
            RamWorkingSetMb: fields[18],
            CommitMb: fields[19],
            Processes: fields[20] is { } n ? (int)n : 0,
            Sources: fields[21] is { } src ? (ProcessReadingSources)(int)src : ProcessReadingSources.None));
        return at;
    }

    private static double?[] Fields(in TelemetrySample sample)
    {
        GpuSample s = sample.Sample;
        ProcessReading game = sample.Game;
        return
        [
            s.TempCoreC, s.TempHotspotC, s.TempMemoryC, s.LoadPct, s.VramAdapterMb, s.CoreClockMhz, s.MemClockMhz,
            s.PowerW, s.FanRpm, s.ThrottleReasons, s.PcieGen, s.PcieWidth,
            sample.System.CpuLoadPct, sample.System.RamUsedMb, sample.System.CpuTempC,
            game.VramDedicatedMb, game.VramSharedMb, game.RamPrivateMb, game.RamWorkingSetMb, game.CommitMb,
            game.IsEmpty ? null : game.Processes, game.IsEmpty ? null : (int)game.Sources,
        ];
    }
}
