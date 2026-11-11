// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Buffers.Binary;
using System.Text;
using FluentAssertions;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Telemetry;
using FrameLedger.Domain.Sessions;
using FrameLedger.Infrastructure.Recording;

namespace FrameLedger.Infrastructure.Tests.Recording;

/// <summary>
/// The <c>.partial</c> and Advanced metrics (owner decisions D101 and D106): a sample's advanced readings and the
/// session's context survive the round trip; with the option off the file is byte for byte what it was; a file cut at
/// every byte still reads up to its last whole chunk; and a chunk of a type this build does not know is passed over.
/// </summary>
public sealed class PartialSessionFileAdvancedTests : IDisposable
{
    private static readonly DateTimeOffset _t0 = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-partial-adv-" + Guid.NewGuid().ToString("N"));

    public PartialSessionFileAdvancedTests() => Directory.CreateDirectory(_dir);

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
        SessionGuid = Guid.Parse("00000000-0000-0000-0000-0000000000a1"),
        StartedAt = _t0,
        QpcEpoch = 1_000_000,
        QpcFrequency = 10_000_000,
        GameId = 7,
        SnapshotId = 3,
        ExePath = @"C:\Games\Title\game.exe",
        Pid = 4242,
        Tier = CaptureTier.NotHooked,
        Mode = CaptureMode.Attach,
        TelemetryDescriptor = "l1+lhm+nvapi",
    };

    private static TelemetrySample Sample(long qpc, params AdvancedReading[] advanced) => new(
        qpc,
        new GpuSample { TakenAt = _t0.AddSeconds(qpc / 10_000_000.0), Layer = TelemetryLayer.Lhm, TempCoreC = 60 },
        Advanced: advanced.Length == 0 ? null : advanced);

    private static AdvancedReading[] CardAndRail() =>
    [
        new("adv.nvapi.pstate", 0),
        new("adv.nvapi.vram.in_use", 1128.652),
        new("adv.lhm.board.voltage.lpc_nct6798d_0_7", -11.97),
    ];

    [Fact]
    public void ASamplesAdvancedReadingsRoundTripAndASampleWithoutAnyHasNone()
    {
        string path = Path.Combine(_dir, "adv.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(path, Header()))
        {
            w.AppendSensors([Sample(1_100_000, CardAndRail()), Sample(1_200_000), Sample(1_300_000, new AdvancedReading("adv.nvapi.pstate", 8))]);
            w.AppendSensors([Sample(1_400_000, new AdvancedReading("adv.nvapi.pstate", 0))]);
        }

        PartialSession p = PartialSessionFile.Read(path)!;

        p.Truncated.Should().BeFalse();
        p.Sensors.Should().HaveCount(4);
        p.Sensors[0].Advanced.Should().Equal(CardAndRail());
        p.Sensors[0].Advanced![2].Value.Should().Be(-11.97, "a negative rail is a reading");
        p.Sensors[1].Advanced.Should().BeNull("a tick the advanced reader did not publish on carries none, and is not given the last one's");
        p.Sensors[2].Advanced.Should().Equal(new AdvancedReading("adv.nvapi.pstate", 8));
        p.Sensors[3].Advanced.Should().Equal(new AdvancedReading("adv.nvapi.pstate", 0));
        p.Sensors.Select(static s => s.Sample.TempCoreC).Should().OnlyContain(static t => t == 60, "the fixed fields are what they always were");
    }

    [Fact]
    public void WithTheOptionOffTheFileIsByteForByteWhatItWasBeforeAdvancedMetricsExisted()
    {
        // No sample carries a reading, no context is given: no chunk 9, no chunk 10. The bytes are compared with the
        // same samples written with a reading attached, which differ by exactly the chunk that carries it.
        string off = Path.Combine(_dir, "off.partial");
        string on = Path.Combine(_dir, "on.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(off, Header()))
        {
            w.AppendSensors([Sample(1_100_000), Sample(1_200_000)]);
            w.AppendNote(_t0, "end");
        }

        using (IPartialSessionWriter w = PartialSessionFile.Create(on, Header()))
        {
            w.AppendSensors([Sample(1_100_000, new AdvancedReading("adv.nvapi.pstate", 0)), Sample(1_200_000)]);
            w.AppendNote(_t0, "end");
        }

        ChunkTypes(File.ReadAllBytes(off)).Should().Equal(1u, 8u, 6u);
        ChunkTypes(File.ReadAllBytes(on)).Should().Equal(1u, 8u, 9u, 6u);
        byte[] offBytes = File.ReadAllBytes(off);
        byte[] onBytes = File.ReadAllBytes(on);
        int advancedChunk = ChunkOffsets(onBytes)[2];
        int advancedLength = 12 + BitConverter.ToInt32(onBytes, advancedChunk + 4);
        onBytes.AsSpan(0, advancedChunk).ToArray().Should().Equal(offBytes.AsSpan(0, advancedChunk).ToArray(), "the sensor chunk does not change when readings ride beside it");
        onBytes.AsSpan(advancedChunk + advancedLength).ToArray().Should().Equal(offBytes.AsSpan(advancedChunk).ToArray());
    }

    [Fact]
    public void TheSessionsContextRoundTripsAndTheLastOneWins()
    {
        var first = new AdvancedSessionData([], [], [], AdvancedSources.Unknown);
        var last = new AdvancedSessionData(
            [new AdvancedFact("nvapi.gpu.rt_cores", Instance: null, AdvancedValue.Of(84))],
            [new AdvancedFact("nvapi.session.vrr_enabled", Instance: null, AdvancedValue.Of(true))],
            [new AdvancedSeriesInfo("adv.lhm.board.voltage.lpc_nct6798d_0_7", "lhm.board.voltage", "Nuvoton NCT6798D - -12V")],
            new AdvancedSources(AdvancedSourceStatus.Reading, NvapiCode: null, AdvancedSourceStatus.Reading, AdvancedSourceStatus.NotElevated));
        string path = Path.Combine(_dir, "context.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(path, Header()))
        {
            w.AppendAdvancedContext(AdvancedJson.OfContext(first));
            w.AppendSensors([Sample(1_100_000, CardAndRail())]);
            w.AppendAdvancedContext(AdvancedJson.OfContext(last));
        }

        PartialSession p = PartialSessionFile.Read(path)!;

        AdvancedSessionData context = AdvancedJson.ParseContext(p.AdvancedContextJson)!;
        context.MachineFacts.Should().ContainSingle().Which.Value.Number.Should().Be(84);
        context.SessionFacts.Should().ContainSingle().Which.Id.Should().Be("nvapi.session.vrr_enabled");
        context.Series.Should().Equal(last.Series);
        context.Sources.Should().Be(last.Sources);
        ChunkTypes(File.ReadAllBytes(path)).Should().Equal(1u, 10u, 8u, 9u, 10u);
    }

    [Fact]
    public void AFileWithNoContextHasNone()
    {
        string path = Path.Combine(_dir, "none.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(path, Header()))
        {
            w.AppendSensors([Sample(1_100_000)]);
        }

        PartialSessionFile.Read(path)!.AdvancedContextJson.Should().BeNull();
    }

    [Fact]
    public void AKillAtEveryByteOffsetYieldsExactlyTheAdvancedChunksThatWereComplete()
    {
        string path = Path.Combine(_dir, "cut.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(path, Header()))
        {
            w.AppendSensors([Sample(1_100_000, CardAndRail()), Sample(1_200_000, new AdvancedReading("adv.nvapi.pstate", 8))]);
            w.AppendAdvancedContext(AdvancedJson.OfContext(new AdvancedSessionData([], [], [], AdvancedSources.Unknown)));
            w.AppendSensors([Sample(1_300_000, new AdvancedReading("adv.nvapi.pstate", 0))]);
        }

        byte[] whole = File.ReadAllBytes(path);
        List<int> starts = ChunkOffsets(whole);
        ChunkTypes(whole).Should().Equal(1u, 8u, 9u, 10u, 8u, 9u);
        int[] ends = [.. starts.Skip(1), whole.Length];

        for (int cut = ends[0]; cut <= whole.Length; cut++)
        {
            PartialSession p = PartialSessionFile.Parse(whole.AsSpan(0, cut))!;
            int complete = ends.Count(e => e <= cut);

            p.Sensors.Should().HaveCount(complete >= 5 ? 3 : complete >= 2 ? 2 : 0, $"cut at {cut}, {complete} complete chunk(s)");
            p.Sensors.Count(static s => s.Advanced is not null).Should().Be(complete >= 6 ? 3 : complete >= 3 ? 2 : 0, $"cut at {cut}: readings arrive with their own chunk and never before it");
            (p.AdvancedContextJson is not null).Should().Be(complete >= 4, $"cut at {cut}");
            p.Truncated.Should().Be(cut != whole.Length && !ends.Contains(cut), $"cut at {cut}");
        }
    }

    [Fact]
    public void AChunkOfATypeThisBuildDoesNotKnowIsPassedOverAndWhatFollowsIsRead()
    {
        // Owner decision D106. Until 2026-11-11 an unknown type ended the read, so a build one release older lost
        // everything after the first chunk a newer build had added.
        string path = Path.Combine(_dir, "unknown.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(path, Header()))
        {
            w.AppendNote(_t0, "before");
        }

        byte[] head = File.ReadAllBytes(path);
        byte[] future = Chunk(type: 4242, Encoding.UTF8.GetBytes("something a later build writes"));
        string tailPath = Path.Combine(_dir, "tail.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(tailPath, Header()))
        {
            w.AppendSensors([Sample(1_100_000, CardAndRail())]);
            w.AppendNote(_t0.AddSeconds(1), "after");
        }

        byte[] tailFile = File.ReadAllBytes(tailPath);
        byte[] tail = tailFile[ChunkOffsets(tailFile)[1]..];

        PartialSession p = PartialSessionFile.Parse([.. head, .. future, .. tail])!;

        p.Truncated.Should().BeFalse("a chunk whose length and checksum are right is not a torn file");
        p.Notes.Select(static n => n.Text).Should().Equal("before", "after");
        p.Sensors.Should().ContainSingle().Which.Advanced.Should().Equal(CardAndRail());
    }

    [Fact]
    public void AnUnknownChunkWhoseChecksumIsWrongStillEndsTheRead()
    {
        string path = Path.Combine(_dir, "torn.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(path, Header()))
        {
            w.AppendNote(_t0, "before");
        }

        byte[] future = Chunk(type: 4242, [1, 2, 3, 4]);
        future[^1] ^= 0xFF;
        byte[] note = Chunk(type: 6, [.. BitConverter.GetBytes(_t0.ToUnixTimeMilliseconds()), .. Encoding.UTF8.GetBytes("after")]);

        PartialSession p = PartialSessionFile.Parse([.. File.ReadAllBytes(path), .. future, .. note])!;

        p.Truncated.Should().BeTrue("skipping is for a chunk that is whole; one that is not is where the file ends");
        p.Notes.Should().ContainSingle().Which.Text.Should().Be("before");
    }

    [Fact]
    public void AnAdvancedChunkThatIsNotWellFormedEndsTheReadLikeAnyKnownChunkThatIsNot()
    {
        string path = Path.Combine(_dir, "bad.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(path, Header()))
        {
            w.AppendSensors([Sample(1_100_000)]);
        }

        // One name declared, and a reading that points at a second one that is not there.
        byte[] payload = [1, 0, 1, 0, (byte)'a', 1, 0, 0, 0, .. BitConverter.GetBytes(1_100_000L), 1, 0, 5, 0, .. BitConverter.GetBytes(1.0)];
        byte[] note = Chunk(type: 6, [.. BitConverter.GetBytes(_t0.ToUnixTimeMilliseconds()), .. Encoding.UTF8.GetBytes("after")]);

        PartialSession p = PartialSessionFile.Parse([.. File.ReadAllBytes(path), .. Chunk(type: 9, payload), .. note])!;

        p.Truncated.Should().BeTrue();
        p.Sensors.Should().ContainSingle().Which.Advanced.Should().BeNull();
        p.Notes.Should().BeEmpty("nothing after a chunk that failed is trusted");
    }

    [Fact]
    public void ReadingsWhoseSampleIsNotInTheFileBelongToNoTickAndAreLeftOut()
    {
        string path = Path.Combine(_dir, "orphan.partial");
        using (IPartialSessionWriter w = PartialSessionFile.Create(path, Header()))
        {
            w.AppendSensors([Sample(1_100_000)]);
        }

        // A well-formed chunk for a QPC no sample has.
        byte[] payload = [1, 0, 1, 0, (byte)'a', 1, 0, 0, 0, .. BitConverter.GetBytes(9_999_999L), 1, 0, 0, 0, .. BitConverter.GetBytes(1.0)];

        PartialSession p = PartialSessionFile.Parse([.. File.ReadAllBytes(path), .. Chunk(type: 9, payload)])!;

        p.Truncated.Should().BeFalse();
        p.Sensors.Should().ContainSingle().Which.Advanced.Should().BeNull();
    }

    /// <summary>One chunk as the format frames it: type, length, payload, then the IEEE CRC-32 of the first three.</summary>
    private static byte[] Chunk(uint type, byte[] payload)
    {
        byte[] chunk = new byte[12 + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(chunk, type);
        BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(4), (uint)payload.Length);
        payload.CopyTo(chunk, 8);
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in chunk.AsSpan(0, 8 + payload.Length))
        {
            crc ^= b;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(8 + payload.Length), crc ^ 0xFFFFFFFFu);
        return chunk;
    }

    private static List<int> ChunkOffsets(byte[] file)
    {
        var offsets = new List<int>();
        for (int at = 0; at < file.Length; at += 12 + BitConverter.ToInt32(file, at + 4))
        {
            offsets.Add(at);
        }

        return offsets;
    }

    private static uint[] ChunkTypes(byte[] file) => [.. ChunkOffsets(file).Select(at => BitConverter.ToUInt32(file, at))];
}
