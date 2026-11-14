// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Input;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Telemetry;
using FrameLedger.Domain.Input;
using FrameLedger.Domain.Sessions;
using FrameLedger.Infrastructure.Recording;

namespace FrameLedger.Infrastructure.Tests.Recording;

/// <summary>
/// The <c>.partial</c> and the input readings (owner decisions D155-D163): a sample's reading survives the round trip
/// with every controller it counts; with the option off the file is byte for byte what it was; a file cut at every byte
/// still reads up to its last whole chunk; and a chunk that is not well formed ends the read.
/// </summary>
public sealed class PartialSessionFileInputTests : IDisposable
{
    private static readonly DateTimeOffset _t0 = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly InputDevice _xbox = new() { Id = 3, Family = ControllerFamily.Xbox, VendorId = ControllerCatalog.Microsoft, ProductId = 0x0B12, Connection = ControllerConnection.Wireless, XInputSlot = 2 };

    private static readonly InputDevice _dualSense = new() { Id = 70_000, Family = ControllerFamily.PlayStation, VendorId = ControllerCatalog.Sony, ProductId = ControllerCatalog.DualSense, Connection = ControllerConnection.Bluetooth };

    private static readonly InputDevice _steam = new() { Id = 5, Family = ControllerFamily.Xbox, VendorId = ControllerCatalog.Valve, ProductId = ControllerCatalog.SteamVirtualGamepad, IsVirtual = true, XInputSlot = 0 };

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-partial-input-" + Guid.NewGuid().ToString("N"));

    public PartialSessionFileInputTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static PartialHeader Header() => new()
    {
        SessionGuid = Guid.Parse("00000000-0000-0000-0000-0000000000b1"),
        StartedAt = _t0,
        QpcEpoch = 1_000_000,
        QpcFrequency = 10_000_000,
        GameId = 7,
        SnapshotId = 3,
        ExePath = @"C:\Games\Title\game.exe",
        Pid = 4242,
        Tier = CaptureTier.NotHooked,
        Mode = CaptureMode.Attach,
        TelemetryDescriptor = "l1+lhm",
    };

    private static TelemetrySample Sample(long qpc, InputReading? input = null) => new(
        qpc,
        new GpuSample { TakenAt = _t0.AddSeconds(qpc / 10_000_000.0), Layer = TelemetryLayer.Lhm, TempCoreC = 60 },
        Input: input);

    /// <summary>A reading with every field set to something a default would not be.</summary>
    private static InputReading Full() => new()
    {
        SpanMs = 1004,
        ObservedMs = 903,
        ForegroundMs = 802,
        ControllerMs = 401,
        KeyboardMouseMs = 200,
        BothMs = 101,
        IdleMs = 100,
        VirtualPad = true,
        Devices =
        [
            new InputDeviceReading(_xbox.Id, Connected: true, ActiveMs: 502, new BatteryReading(BatteryState.Discharging, BatteryScale.FourLevels, 2), Connects: 1, Disconnects: 0),
            new InputDeviceReading(_dualSense.Id, Connected: false, ActiveMs: 0, new BatteryReading(BatteryState.Charging, BatteryScale.Percent, 100), Connects: 2, Disconnects: 3),
            new InputDeviceReading(_steam.Id, Connected: true, ActiveMs: 490, Battery: null, Connects: 0, Disconnects: 0),
        ],
        Table = [_xbox, _dualSense, _steam],
        Health = new InputHealth
        {
            RawInputRegistered = true,
            XInputAvailable = true,
            LastInputAvailable = true,
            RawReports = 5_000_000_000,
            RawReportsUnread = 7,
            XInputReads = 1234,
            XInputSilent = 5,
            Slices = 10,
            ThreadCpuMs = 3,
            Faults = 1,
        },
    };

    private static InputReading Quiet() => new() { SpanMs = 1000, ObservedMs = 1000, ForegroundMs = 1000, IdleMs = 1000, Health = new InputHealth { LastInputAvailable = true, Slices = 20 } };

    [Fact]
    public void ASamplesReadingRoundTripsWithEveryControllerAndASampleWithoutOneHasNone()
    {
        string path = Path.Combine(_dir, "input.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(path, Header()))
        {
            w.AppendSensors([Sample(1_100_000, Full()), Sample(1_200_000), Sample(1_300_000, Quiet())]);
            w.AppendSensors([Sample(1_400_000, Full() with { VirtualPad = false })]);
        }

        PartialSession p = PartialSessionFile.Read(path)!;

        p.Truncated.Should().BeFalse();
        p.Sensors.Should().HaveCount(4);
        p.Sensors[0].Input.Should().BeEquivalentTo(Full(), static o => o.WithStrictOrdering());
        p.Sensors[0].Input!.Table[1].Model.Should().Be("DualSense", "a controller is what its two ids say, and they travel with the reading");
        p.Sensors[1].Input.Should().BeNull("a tick the reader took no slice in carries none, and is not given the last one's");
        p.Sensors[2].Input.Should().BeEquivalentTo(Quiet());
        p.Sensors[3].Input!.VirtualPad.Should().BeFalse();
        PartialSessionFileAdvancedTests.ChunkTypes(File.ReadAllBytes(path)).Should().Equal(1u, 8u, 11u, 8u, 11u);
    }

    [Fact]
    public void WithTheOptionOffTheFileIsByteForByteWhatItWasBeforeTheReadingExisted()
    {
        string off = Path.Combine(_dir, "off.partial");
        string on = Path.Combine(_dir, "on.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(off, Header()))
        {
            w.AppendSensors([Sample(1_100_000), Sample(1_200_000)]);
            w.AppendNote(_t0, "end");
        }

        using (IPartialSessionWriter w = PartialSessionFile.Create(on, Header()))
        {
            w.AppendSensors([Sample(1_100_000, Quiet()), Sample(1_200_000)]);
            w.AppendNote(_t0, "end");
        }

        byte[] offBytes = File.ReadAllBytes(off);
        byte[] onBytes = File.ReadAllBytes(on);
        PartialSessionFileAdvancedTests.ChunkTypes(offBytes).Should().Equal(1u, 8u, 6u);
        PartialSessionFileAdvancedTests.ChunkTypes(onBytes).Should().Equal(1u, 8u, 11u, 6u);
        int inputChunk = PartialSessionFileAdvancedTests.ChunkOffsets(onBytes)[2];
        int inputLength = 12 + BitConverter.ToInt32(onBytes, inputChunk + 4);
        onBytes.AsSpan(0, inputChunk).ToArray().Should().Equal(offBytes.AsSpan(0, inputChunk).ToArray(), "the sensor chunk does not change when a reading rides beside it");
        onBytes.AsSpan(inputChunk + inputLength).ToArray().Should().Equal(offBytes.AsSpan(inputChunk).ToArray());
    }

    [Fact]
    public void AKillAtEveryByteOffsetYieldsExactlyTheReadingsWhoseChunkWasComplete()
    {
        string path = Path.Combine(_dir, "cut.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(path, Header()))
        {
            w.AppendSensors([Sample(1_100_000, Full()), Sample(1_200_000, Quiet())]);
            w.AppendSensors([Sample(1_300_000, Full())]);
        }

        byte[] whole = File.ReadAllBytes(path);
        List<int> starts = PartialSessionFileAdvancedTests.ChunkOffsets(whole);
        PartialSessionFileAdvancedTests.ChunkTypes(whole).Should().Equal(1u, 8u, 11u, 8u, 11u);
        int[] ends = [.. starts.Skip(1), whole.Length];

        for (int cut = ends[0]; cut <= whole.Length; cut++)
        {
            PartialSession p = PartialSessionFile.Parse(whole.AsSpan(0, cut))!;
            int complete = ends.Count(e => e <= cut);

            p.Sensors.Should().HaveCount(complete >= 4 ? 3 : complete >= 2 ? 2 : 0, $"cut at {cut}, {complete} complete chunk(s)");
            p.Sensors.Count(static s => s.Input is not null).Should().Be(complete >= 5 ? 3 : complete >= 3 ? 2 : 0, $"cut at {cut}: a reading arrives with its own chunk and never before it");
            p.Truncated.Should().Be(cut != whole.Length && !ends.Contains(cut), $"cut at {cut}");
        }
    }

    [Fact]
    public void AnInputChunkThatIsNotWellFormedEndsTheReadLikeAnyKnownChunkThatIsNot()
    {
        string path = Path.Combine(_dir, "bad.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(path, Header()))
        {
            w.AppendSensors([Sample(1_100_000, Quiet())]);
        }

        byte[] file = File.ReadAllBytes(path);
        int at = PartialSessionFileAdvancedTests.ChunkOffsets(file)[2];
        byte[] good = file.AsSpan(at + 8, BitConverter.ToInt32(file, at + 4)).ToArray();
        byte[] head = file[..at];

        byte[][] bad =
        [
            [],
            [1, 0],
            good[..^1], // a sample cut short
            [.. good, 0], // a byte too many
            [2, 0, 0, 0, .. good[4..]], // two samples declared, one there
            [.. good[..^1], 1], // a controller declared and not there
        ];

        foreach (byte[] payload in bad)
        {
            PartialSession p = PartialSessionFile.Parse([.. head, .. PartialSessionFileAdvancedTests.Chunk(type: 11, payload)])!;

            p.Truncated.Should().BeTrue($"a payload of {payload.Length} byte(s)");
            p.Sensors.Should().ContainSingle().Which.Input.Should().BeNull();
        }

        PartialSessionFile.Parse([.. head, .. PartialSessionFileAdvancedTests.Chunk(type: 11, good)])!.Sensors[0].Input.Should().BeEquivalentTo(Quiet(), "the same chunk, whole");
    }

    [Fact]
    public void AReadingWhoseSampleIsNotInTheFileBelongsToNoTickAndIsLeftOut()
    {
        string source = Path.Combine(_dir, "source.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(source, Header()))
        {
            w.AppendSensors([Sample(9_999_999, Quiet())]);
        }

        string path = Path.Combine(_dir, "orphan.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(path, Header()))
        {
            w.AppendSensors([Sample(1_100_000)]);
        }

        byte[] other = File.ReadAllBytes(source);
        byte[] orphan = other[PartialSessionFileAdvancedTests.ChunkOffsets(other)[2]..];

        PartialSession p = PartialSessionFile.Parse([.. File.ReadAllBytes(path), .. orphan])!;

        p.Truncated.Should().BeFalse();
        p.Sensors.Should().ContainSingle().Which.Input.Should().BeNull();
    }

    /// <summary>A later build may know a maker, a connection or a battery state this one has no name for: it reads as "do not know", and the rest of the reading is kept.</summary>
    [Fact]
    public void AValueThisBuildHasNoNameForReadsAsUnknownAndTheRestIsKept()
    {
        string path = Path.Combine(_dir, "later.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(path, Header()))
        {
            w.AppendSensors([Sample(1_100_000, Full() with { Devices = [Full().Devices[0]], Table = [_xbox] })]);
        }

        byte[] file = File.ReadAllBytes(path);
        int at = PartialSessionFileAdvancedTests.ChunkOffsets(file)[2];
        byte[] payload = file.AsSpan(at + 8, BitConverter.ToInt32(file, at + 4)).ToArray();
        int device = payload.Length - 23;
        payload[device + 4] = 200; // the family
        payload[device + 9] = 201; // the connection
        payload[device + 16] = 202; // the battery's state
        payload[device + 17] = 203; // and its scale

        InputReading reading = PartialSessionFile.Parse([.. file[..at], .. PartialSessionFileAdvancedTests.Chunk(type: 11, payload)])!.Sensors[0].Input!;

        reading.Table[0].Family.Should().Be(ControllerFamily.Other);
        reading.Table[0].Connection.Should().Be(ControllerConnection.Unknown);
        reading.Devices[0].Battery.Should().Be(new BatteryReading(BatteryState.Unknown, BatteryScale.None, 2));
        reading.Devices[0].ActiveMs.Should().Be(502);
        reading.Table[0].XInputSlot.Should().Be(2);
    }

    /// <summary>A controller the reading counts and does not describe cannot be written whole, and is left out; ids past sixteen bits are held to them.</summary>
    [Fact]
    public void AControllerWithNoDescriptionIsLeftOutAndOutOfRangeValuesAreHeld()
    {
        var wide = new InputDevice { Id = 9, VendorId = 0x12345, ProductId = -4, XInputSlot = 300 };
        var reading = new InputReading
        {
            Devices =
            [
                new InputDeviceReading(404, Connected: true, ActiveMs: 10, Battery: null, Connects: 0, Disconnects: 0),
                new InputDeviceReading(9, Connected: true, ActiveMs: 20, new BatteryReading(BatteryState.Discharging, BatteryScale.Percent, 900), Connects: 70_000, Disconnects: -1),
            ],
            Table = [wide],
        };
        string path = Path.Combine(_dir, "held.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(path, Header()))
        {
            w.AppendSensors([Sample(1_100_000, reading)]);
        }

        InputReading back = PartialSessionFile.Read(path)!.Sensors[0].Input!;

        back.Devices.Should().ContainSingle().Which.Should().Be(
            new InputDeviceReading(9, Connected: true, ActiveMs: 20, new BatteryReading(BatteryState.Discharging, BatteryScale.Percent, 255), Connects: 65_535, Disconnects: 0));
        back.Table.Should().ContainSingle().Which.Should().BeEquivalentTo(wide with { VendorId = 65_535, ProductId = 0, XInputSlot = 255 });
    }
}
