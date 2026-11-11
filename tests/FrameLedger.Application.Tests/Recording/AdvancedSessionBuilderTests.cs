// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Telemetry;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>
/// What a session stores of Advanced metrics (owner decision D102): a series per advanced reading, aligned to the
/// sensors' ticks with NaN where a tick had none; the row of its facts, statistics and directory; and nothing at all
/// when the option was off.
/// </summary>
public sealed class AdvancedSessionBuilderTests
{
    private static readonly AdvancedSources _sources = new(AdvancedSourceStatus.Reading, NvapiCode: null, AdvancedSourceStatus.Reading, AdvancedSourceStatus.NotElevated);

    private static readonly AdvancedSessionData _data = new(
        [new AdvancedFact("nvapi.gpu.rt_cores", Instance: null, AdvancedValue.Of(84))],
        [new AdvancedFact("nvapi.session.vrr_enabled", Instance: null, AdvancedValue.Of(true))],
        [new AdvancedSeriesInfo("adv.lhm.board.voltage.lpc_0_7", "lhm.board.voltage", "Chip - -12V"), new AdvancedSeriesInfo("adv.lhm.gpu.fan.gone", "lhm.gpu.fan", "a series no sample carried")],
        _sources);

    /// <summary>Five ticks: the card's state on every one, a rail on the first and the last, nothing on the third.</summary>
    private static List<TelemetrySample> Samples()
    {
        List<TelemetrySample> samples = SessionFixtures.Sensors(5);
        samples[0] = samples[0] with { Advanced = [new AdvancedReading("adv.nvapi.pstate", 0), new AdvancedReading("adv.lhm.board.voltage.lpc_0_7", -11.97)] };
        samples[1] = samples[1] with { Advanced = [new AdvancedReading("adv.nvapi.pstate", 8)] };
        samples[3] = samples[3] with { Advanced = [new AdvancedReading("adv.nvapi.pstate", 8)] };
        samples[4] = samples[4] with { Advanced = [new AdvancedReading("adv.nvapi.pstate", 0), new AdvancedReading("adv.lhm.board.voltage.lpc_0_7", -12.03)] };
        return samples;
    }

    private static float[] Decode(SensorBlob blob) => MemoryMarshal.Cast<byte, float>(blob.Data.Span).ToArray();

    [Fact]
    public void WithTheOptionOffNothingIsStoredAtAll()
    {
        (IReadOnlyList<SensorBlob> blobs, AdvancedSessionRow? row) = AdvancedSessionBuilder.Build(SessionFixtures.Sensors(5), data: null, new RawSeriesCodec());

        blobs.Should().BeEmpty();
        row.Should().BeNull("no row is how the App knows the option was off when the session was recorded");
    }

    [Fact]
    public void EachSeriesIsAlignedToTheSensorsTicksWithNaNWhereATickHadNoReading()
    {
        (IReadOnlyList<SensorBlob> blobs, _) = AdvancedSessionBuilder.Build(Samples(), _data, new RawSeriesCodec());

        blobs.Select(static b => b.Series).Should().Equal("adv.nvapi.pstate", "adv.lhm.board.voltage.lpc_0_7");
        blobs.Should().OnlyContain(static b => b.Hz == 1 && string.Equals(b.Codec, "raw-le-test", StringComparison.Ordinal));
        float[] pstate = Decode(blobs[0]);
        float[] rail = Decode(blobs[1]);
        pstate.Should().HaveCount(5);
        pstate[0].Should().Be(0f, "P0 is a state, not a gap");
        pstate[1].Should().Be(8f);
        float.IsNaN(pstate[2]).Should().BeTrue("the third tick carried nothing: it is not given the second's value");
        rail[0].Should().Be(-11.97f, "a negative rail is a reading");
        rail.Skip(1).Take(3).Should().OnlyContain(static v => float.IsNaN(v), "the sensor library is read every fifth tick");
        rail[4].Should().Be(-12.03f);
    }

    [Fact]
    public void TheRowHoldsTheFactsTheSourcesTheStatisticsAndTheDirectoryOfWhatWasStored()
    {
        (_, AdvancedSessionRow? built) = AdvancedSessionBuilder.Build(Samples(), _data, new RawSeriesCodec());

        AdvancedSessionRow row = built.Should().BeOfType<AdvancedSessionRow>().Subject;
        row.MachineFactsJson.Should().Be("[{\"id\":\"nvapi.gpu.rt_cores\",\"number\":84}]");
        row.MachineHash.Should().Be(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(row.MachineFactsJson!))));
        (IReadOnlyList<AdvancedFact> facts, AdvancedSources sources) = AdvancedJson.ParseSession(row.FactsJson);
        facts.Should().ContainSingle().Which.Id.Should().Be("nvapi.session.vrr_enabled");
        sources.Should().Be(_sources);
        IReadOnlyDictionary<string, SensorSeriesStats> stats = AdvancedJson.ParseStats(row.StatsJson);
        stats["adv.nvapi.pstate"].Should().Be(new SensorSeriesStats(4, 4, 4, 0, 8), "four ticks had a reading; the one that had none is not a zero in the mean");
        stats["adv.lhm.board.voltage.lpc_0_7"].N.Should().Be(2);
        stats["adv.lhm.board.voltage.lpc_0_7"].Min.Should().BeApproximately(-12.03, 1e-5);
        (IReadOnlyList<AdvancedSeriesInfo> directory, int dropped) = AdvancedJson.ParseSeries(row.SeriesJson);
        directory.Should().ContainSingle("only a series that was stored is in the directory").Which.Label.Should().Be("Chip - -12V");
        dropped.Should().Be(0);
    }

    [Fact]
    public void ASessionWithTheOptionOnAndNoReadingStillSaysWhichSourcesAnsweredAndWhy()
    {
        var nothing = new AdvancedSessionData([], [], [], new AdvancedSources(AdvancedSourceStatus.NotPresent, NvapiCode: -6, AdvancedSourceStatus.Faulted, AdvancedSourceStatus.NotElevated));

        (IReadOnlyList<SensorBlob> blobs, AdvancedSessionRow? row) = AdvancedSessionBuilder.Build(SessionFixtures.Sensors(5), nothing, new RawSeriesCodec());

        blobs.Should().BeEmpty();
        AdvancedSessionRow stored = row.Should().BeOfType<AdvancedSessionRow>("the option was on: what is stored is that nothing answered, and why").Subject;
        stored.MachineFactsJson.Should().BeNull();
        stored.MachineHash.Should().BeNull();
        stored.StatsJson.Should().BeNull();
        AdvancedJson.ParseSession(stored.FactsJson).Sources.Nvapi.Should().Be(AdvancedSourceStatus.NotPresent);
    }

    [Fact]
    public void TwoSessionsOnTheSamePcHaveTheSameMachineHashAndAChangedFactChangesIt()
    {
        AdvancedSessionRow first = AdvancedSessionBuilder.Build(Samples(), _data, new RawSeriesCodec()).Row!;
        AdvancedSessionRow second = AdvancedSessionBuilder.Build(SessionFixtures.Sensors(3), _data with { SessionFacts = [] }, new RawSeriesCodec()).Row!;
        AdvancedSessionRow changed = AdvancedSessionBuilder.Build(Samples(), _data with { MachineFacts = [new AdvancedFact("nvapi.gpu.rt_cores", Instance: null, AdvancedValue.Of(96))] }, new RawSeriesCodec()).Row!;

        second.MachineHash.Should().Be(first.MachineHash, "the hash is of the PC's facts alone, so every session on it finds one row");
        changed.MachineHash.Should().NotBe(first.MachineHash);
    }

    [Fact]
    public void AFactTheCatalogDoesNotSayIsReadNeverReachesTheRow()
    {
        // D104 at the door of the ledger.
        AdvancedSessionData leaky = _data with
        {
            MachineFacts = [.. _data.MachineFacts, new AdvancedFact("nvapi.gpu.board_serial", Instance: null, AdvancedValue.OfText("0123456789"))],
            SessionFacts = [.. _data.SessionFacts, new AdvancedFact("sl.constants.camera", Instance: null, AdvancedValue.OfText("1 2 3"))],
        };

        AdvancedSessionRow row = AdvancedSessionBuilder.Build(Samples(), leaky, new RawSeriesCodec()).Row!;

        row.MachineFactsJson.Should().NotContain("board_serial").And.NotContain("0123456789");
        row.FactsJson.Should().NotContain("camera");
        row.MachineHash.Should().Be(AdvancedSessionBuilder.Build(Samples(), _data, new RawSeriesCodec()).Row!.MachineHash, "what is not stored does not change the hash");
    }

    [Fact]
    public void ANameWithoutThePrefixANonNumberAndASeriesBeyondTheMostAreNotStored()
    {
        List<TelemetrySample> samples = SessionFixtures.Sensors(2);
        var many = new List<AdvancedReading> { new("gpu_temp", 99), new("adv.nvapi.pstate", double.NaN), new("adv.nvapi.clock.video", double.PositiveInfinity) };
        many.AddRange(Enumerable.Range(0, AdvancedSessionBuilder.MaxSeries + 3).Select(static i => new AdvancedReading($"adv.lhm.cpu.load.c_{i}", i)));
        samples[0] = samples[0] with { Advanced = many };

        (IReadOnlyList<SensorBlob> blobs, AdvancedSessionRow? row) = AdvancedSessionBuilder.Build(samples, _data, new RawSeriesCodec());

        blobs.Should().HaveCount(AdvancedSessionBuilder.MaxSeries);
        blobs.Should().NotContain(static b => string.Equals(b.Series, "gpu_temp", StringComparison.Ordinal), "a name without the prefix could be taken for a fixed series");
        Decode(blobs.Single(static b => string.Equals(b.Series, "adv.nvapi.pstate", StringComparison.Ordinal))).Should().OnlyContain(static v => float.IsNaN(v), "a value that is not a number is not a reading");
        AdvancedJson.ParseSeries(row!.SeriesJson).Dropped.Should().Be(5, "two series were named before the first of the family, so five of them did not fit: counted, not stored");
        AdvancedJson.ParseStats(row.StatsJson).Should().NotContainKey("adv.nvapi.pstate", "a series with no reading has no statistics");
    }

    [Fact]
    public void TheFinalizerStoresThemWithBothTiersAndLeavesAFixedSessionAsItWas()
    {
        var finalizer = new SessionFinalizer(new FakeSessionRepository(), new RawSeriesCodec());
        FinalizeInput off = new() { Skeleton = SessionFixtures.Skeleton(tier: CaptureTier.NotHooked), Sensors = SessionFixtures.Sensors(5) };
        FinalizeInput on = off with { Sensors = Samples(), Advanced = _data };

        FinalizedSession before = finalizer.Build(off);
        FinalizedSession unhooked = finalizer.Build(on);
        FinalizedSession hooked = finalizer.Build(on with
        {
            Skeleton = SessionFixtures.Skeleton(),
            Hooked = SessionFixtures.Hooked(SessionFixtures.Stream(600, Shared.FlMeasured.OutputRes | Shared.FlMeasured.PresentArgs)),
        });

        before.Advanced.Should().BeNull();
        before.Sensors.Should().NotContain(static s => s.Series.StartsWith("adv.", StringComparison.Ordinal));
        unhooked.Advanced.Should().NotBeNull("a session that was not hooked has them all the same: the Agent reads them, not a hook");
        unhooked.Sensors.Select(static s => s.Series).Should().StartWith(before.Sensors.Select(static s => s.Series), "the fixed series come first and are untouched");
        unhooked.Sensors.Select(static s => s.Series).Should().EndWith(["adv.nvapi.pstate", "adv.lhm.board.voltage.lpc_0_7"]);
        unhooked.Row.Should().BeEquivalentTo(before.Row, "no column of sessions knows of them (D102)");
        hooked.Advanced.Should().NotBeNull();
        hooked.Sensors.Should().Contain(static s => string.Equals(s.Series, "adv.nvapi.pstate", StringComparison.Ordinal));
    }

    [Fact]
    public void TheWriterPutsTheContextInTheFileOnceAndAgainOnlyWhenItChanges()
    {
        var store = new FakePartialSessionStore();
        var entry = (FakePartialSessionStore.Entry)store.Create(new PartialHeader
        {
            SessionGuid = Guid.NewGuid(),
            StartedAt = SessionFixtures.StartedAt,
            QpcEpoch = SessionFixtures.QpcEpoch,
            QpcFrequency = SessionFixtures.QpcFrequency,
            GameId = 7,
            SnapshotId = 3,
            ExePath = @"C:\Games\T\t.exe",
            Pid = 1,
            Tier = CaptureTier.NotHooked,
            Mode = CaptureMode.Attach,
        });
        using var writer = new PartialSessionWriter(entry, TimeProvider.System, TimeSpan.Zero);

        writer.Advanced.Should().BeNull();
        writer.OnTick(CaptureProgress.Unhooked, force: true);
        entry.AdvancedContexts.Should().BeEmpty("with the option off the file carries none");

        writer.SetAdvancedContext(_data);
        writer.OnTick(CaptureProgress.Unhooked, force: true);
        writer.OnTick(CaptureProgress.Unhooked, force: true);
        entry.AdvancedContexts.Should().ContainSingle("a context that stays the same is in the file once");

        writer.SetAdvancedContext(_data with { SessionFacts = [] });
        writer.OnTick(CaptureProgress.Unhooked, force: true);

        entry.AdvancedContexts.Should().HaveCount(2);
        writer.Advanced!.SessionFacts.Should().BeEmpty();
        AdvancedJson.ParseContext(entry.AdvancedContexts[^1])!.MachineFacts.Should().ContainSingle();
        FluentActions.Invoking(() => writer.SetAdvancedContext(null!)).Should().Throw<ArgumentNullException>();
    }
}
