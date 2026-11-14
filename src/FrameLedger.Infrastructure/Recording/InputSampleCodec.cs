// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Buffers.Binary;
using FrameLedger.Application.Input;
using FrameLedger.Application.Telemetry;
using FrameLedger.Domain.Input;

namespace FrameLedger.Infrastructure.Recording;

/// <summary>
/// The input readings of a batch of telemetry samples, as one chunk of a <c>.partial</c> file (owner decisions
/// D155-D163, <c>06_DATA_MODEL</c> §<c>.partial</c>): so a session that is recovered after the Agent died has what it
/// was played with, as a finished one does.
/// </summary>
/// <remarks>
/// <para>
/// A chunk of its own, after the sensors' and the advanced one, for the reason <see cref="AdvancedSampleCodec"/> has
/// one: the sensor chunk's form is fixed, and a reading is attached to its sample by the QPC the sample already has. A
/// session recorded with the option off never writes one, and its file is byte for byte what it was.
/// </para>
/// <para>
/// <b>Each chunk stands alone</b>: every controller a sample counts is described in that sample - its maker, its two
/// USB ids, its connection - so a file cut anywhere still reads up to its last whole chunk.
/// </para>
/// <para>
/// Layout, little-endian: <c>u32 samples</c>, then per sample <c>i64 qpc</c>, seven <c>i32</c> (span, observed, in
/// front, controller, keyboard and mouse, both, no input), <c>u8 flags</c> (1 a virtual pad), the health (<c>u8</c>
/// answers: 1 raw input registered, 2 XInput there, 4 last input readable; six <c>i64</c> counts; <c>i32 faults</c>),
/// <c>u8 controllers</c> and per controller 23 bytes: <c>i32 id</c>, <c>u8 family</c>, <c>u16 vendor</c>,
/// <c>u16 product</c>, <c>u8 connection</c>, <c>u8 flags</c> (1 virtual, 2 one of XInput's pads, 4 connected, 8 a
/// battery), <c>u8 slot</c>, <c>i32 active ms</c>, <c>u8</c> battery state, scale and level, <c>u16 connects</c>,
/// <c>u16 disconnects</c>.
/// </para>
/// </remarks>
internal static class InputSampleCodec
{
    private const int _sampleBytes = 8 + (7 * 4) + 1 + _healthBytes + 1;
    private const int _healthBytes = 1 + (6 * 8) + 4;
    private const int _deviceBytes = 23;

    private const byte _virtualPad = 1;

    private const byte _rawRegistered = 1;
    private const byte _xinputAvailable = 2;
    private const byte _lastInputAvailable = 4;

    private const byte _deviceVirtual = 1;
    private const byte _deviceXInput = 2;
    private const byte _deviceConnected = 4;
    private const byte _deviceBattery = 8;

    /// <summary>The chunk for the samples of <paramref name="samples"/> that carry an input reading, or null when none does.</summary>
    public static byte[]? Write(ReadOnlySpan<TelemetrySample> samples)
    {
        int carrying = 0;
        int bytes = 4;
        foreach (ref readonly TelemetrySample sample in samples)
        {
            if (sample.Input is { } reading)
            {
                carrying++;
                bytes += _sampleBytes + (Devices(reading).Count * _deviceBytes);
            }
        }

        if (carrying == 0)
        {
            return null;
        }

        byte[] payload = new byte[bytes];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, (uint)carrying);
        int at = 4;
        foreach (ref readonly TelemetrySample sample in samples)
        {
            if (sample.Input is { } reading)
            {
                at += WriteSample(payload.AsSpan(at), sample.QpcTicks, reading);
            }
        }

        return payload;
    }

    /// <summary>The reading of each sample in a chunk, by the sample's QPC; false when the chunk is not well formed.</summary>
    public static bool TryRead(ReadOnlySpan<byte> payload, List<(long Qpc, InputReading Reading)> into)
    {
        if (payload.Length < 4)
        {
            return false;
        }

        uint samples = BinaryPrimitives.ReadUInt32LittleEndian(payload);
        int at = 4;
        var read = new List<(long, InputReading)>();
        for (uint s = 0; s < samples; s++)
        {
            if (payload.Length - at < _sampleBytes)
            {
                return false;
            }

            int devices = payload[at + _sampleBytes - 1];
            if (payload.Length - at - _sampleBytes < devices * _deviceBytes)
            {
                return false;
            }

            read.Add(ReadSample(payload.Slice(at, _sampleBytes + (devices * _deviceBytes)), devices));
            at += _sampleBytes + (devices * _deviceBytes);
        }

        if (at != payload.Length)
        {
            return false;
        }

        into.AddRange(read);
        return true;
    }

    /// <summary>The controllers of a reading that can be written: each with its description, at most 255.</summary>
    private static List<(InputDevice Device, InputDeviceReading Reading)> Devices(InputReading reading)
    {
        var devices = new List<(InputDevice, InputDeviceReading)>(reading.Devices.Count);
        foreach (InputDeviceReading d in reading.Devices)
        {
            if (devices.Count < byte.MaxValue && reading.Table.FirstOrDefault(t => t.Id == d.DeviceId) is { } device)
            {
                devices.Add((device, d));
            }
        }

        return devices;
    }

    private static int WriteSample(Span<byte> to, long qpc, InputReading reading)
    {
        BinaryPrimitives.WriteInt64LittleEndian(to, qpc);
        int at = 8;
        foreach (int ms in (ReadOnlySpan<int>)[reading.SpanMs, reading.ObservedMs, reading.ForegroundMs, reading.ControllerMs, reading.KeyboardMouseMs, reading.BothMs, reading.IdleMs])
        {
            BinaryPrimitives.WriteInt32LittleEndian(to[at..], ms);
            at += 4;
        }

        to[at++] = reading.VirtualPad ? _virtualPad : (byte)0;

        InputHealth h = reading.Health;
        to[at++] = (byte)((h.RawInputRegistered ? _rawRegistered : 0) | (h.XInputAvailable ? _xinputAvailable : 0) | (h.LastInputAvailable ? _lastInputAvailable : 0));
        foreach (long count in (ReadOnlySpan<long>)[h.RawReports, h.RawReportsUnread, h.XInputReads, h.XInputSilent, h.Slices, h.ThreadCpuMs])
        {
            BinaryPrimitives.WriteInt64LittleEndian(to[at..], count);
            at += 8;
        }

        BinaryPrimitives.WriteInt32LittleEndian(to[at..], h.Faults);
        at += 4;

        List<(InputDevice Device, InputDeviceReading Reading)> devices = Devices(reading);
        to[at++] = (byte)devices.Count;
        foreach ((InputDevice device, InputDeviceReading d) in devices)
        {
            WriteDevice(to.Slice(at, _deviceBytes), device, d);
            at += _deviceBytes;
        }

        return at;
    }

    private static void WriteDevice(Span<byte> to, InputDevice device, InputDeviceReading d)
    {
        BinaryPrimitives.WriteInt32LittleEndian(to, device.Id);
        to[4] = (byte)device.Family;
        BinaryPrimitives.WriteUInt16LittleEndian(to[5..], (ushort)Math.Clamp(device.VendorId, 0, ushort.MaxValue));
        BinaryPrimitives.WriteUInt16LittleEndian(to[7..], (ushort)Math.Clamp(device.ProductId, 0, ushort.MaxValue));
        to[9] = (byte)device.Connection;
        to[10] = (byte)((device.IsVirtual ? _deviceVirtual : 0)
            | (device.XInputSlot is not null ? _deviceXInput : 0)
            | (d.Connected ? _deviceConnected : 0)
            | (d.Battery is not null ? _deviceBattery : 0));
        to[11] = (byte)Math.Clamp(device.XInputSlot ?? 0, 0, byte.MaxValue);
        BinaryPrimitives.WriteInt32LittleEndian(to[12..], d.ActiveMs);
        to[16] = (byte)(d.Battery?.State ?? BatteryState.Unknown);
        to[17] = (byte)(d.Battery?.Scale ?? BatteryScale.None);
        to[18] = (byte)Math.Clamp(d.Battery?.Level ?? 0, 0, byte.MaxValue);
        BinaryPrimitives.WriteUInt16LittleEndian(to[19..], (ushort)Math.Clamp(d.Connects, 0, ushort.MaxValue));
        BinaryPrimitives.WriteUInt16LittleEndian(to[21..], (ushort)Math.Clamp(d.Disconnects, 0, ushort.MaxValue));
    }

    private static (long Qpc, InputReading Reading) ReadSample(ReadOnlySpan<byte> from, int deviceCount)
    {
        long qpc = BinaryPrimitives.ReadInt64LittleEndian(from);
        int at = 8;
        Span<int> ms = stackalloc int[7];
        for (int i = 0; i < ms.Length; i++)
        {
            ms[i] = BinaryPrimitives.ReadInt32LittleEndian(from[at..]);
            at += 4;
        }

        bool virtualPad = (from[at++] & _virtualPad) != 0;
        byte answers = from[at++];
        Span<long> counts = stackalloc long[6];
        for (int i = 0; i < counts.Length; i++)
        {
            counts[i] = BinaryPrimitives.ReadInt64LittleEndian(from[at..]);
            at += 8;
        }

        int faults = BinaryPrimitives.ReadInt32LittleEndian(from[at..]);
        at += 4 + 1;

        var devices = new InputDeviceReading[deviceCount];
        var table = new InputDevice[deviceCount];
        for (int i = 0; i < deviceCount; i++)
        {
            (table[i], devices[i]) = ReadDevice(from.Slice(at, _deviceBytes));
            at += _deviceBytes;
        }

        return (qpc, new InputReading
        {
            SpanMs = ms[0],
            ObservedMs = ms[1],
            ForegroundMs = ms[2],
            ControllerMs = ms[3],
            KeyboardMouseMs = ms[4],
            BothMs = ms[5],
            IdleMs = ms[6],
            VirtualPad = virtualPad,
            Devices = devices,
            Table = table,
            Health = new InputHealth
            {
                RawInputRegistered = (answers & _rawRegistered) != 0,
                XInputAvailable = (answers & _xinputAvailable) != 0,
                LastInputAvailable = (answers & _lastInputAvailable) != 0,
                RawReports = counts[0],
                RawReportsUnread = counts[1],
                XInputReads = counts[2],
                XInputSilent = counts[3],
                Slices = counts[4],
                ThreadCpuMs = counts[5],
                Faults = faults,
            },
        });
    }

    private static (InputDevice Device, InputDeviceReading Reading) ReadDevice(ReadOnlySpan<byte> from)
    {
        byte flags = from[10];
        int id = BinaryPrimitives.ReadInt32LittleEndian(from);
        var device = new InputDevice
        {
            Id = id,

            // A value this build has no name for was written by a later one: it reads as the word for "do not know".
            Family = Known<ControllerFamily>(from[4]),
            VendorId = BinaryPrimitives.ReadUInt16LittleEndian(from[5..]),
            ProductId = BinaryPrimitives.ReadUInt16LittleEndian(from[7..]),
            Connection = Known<ControllerConnection>(from[9]),
            IsVirtual = (flags & _deviceVirtual) != 0,
            XInputSlot = (flags & _deviceXInput) != 0 ? from[11] : null,
        };

        BatteryReading? battery = (flags & _deviceBattery) != 0
            ? new BatteryReading(Known<BatteryState>(from[16]), Known<BatteryScale>(from[17]), from[18])
            : null;
        return (device, new InputDeviceReading(
            id,
            Connected: (flags & _deviceConnected) != 0,
            ActiveMs: BinaryPrimitives.ReadInt32LittleEndian(from[12..]),
            battery,
            Connects: BinaryPrimitives.ReadUInt16LittleEndian(from[19..]),
            Disconnects: BinaryPrimitives.ReadUInt16LittleEndian(from[21..])));
    }

    private static T Known<T>(byte value)
        where T : struct, Enum
    {
        T parsed = (T)Enum.ToObject(typeof(T), value);
        return Enum.IsDefined(parsed) ? parsed : default;
    }
}
