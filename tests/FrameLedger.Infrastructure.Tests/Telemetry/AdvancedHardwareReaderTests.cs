// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Advanced;
using FrameLedger.Infrastructure.Telemetry;

namespace FrameLedger.Infrastructure.Tests.Telemetry;

/// <summary>
/// One tick of "Advanced metrics" over scripted sources: what each source's state is and why, what a tick carries,
/// and when a source that stops answering is initialised again or given up.
/// </summary>
public sealed class AdvancedHardwareReaderTests
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Rig
    {
        public TimeProvider Time { get; } = new FixedClock(new DateTimeOffset(2026, 11, 11, 9, 0, 0, TimeSpan.Zero));

        public List<FakeNvapiAdvancedBridge> Bridges { get; } = [];

        public List<LhmGroups> GroupsAsked { get; } = [];

        public List<string> Notes { get; } = [];

        public int NvapiInit { get; set; }

        public bool NvapiFactoryThrows { get; set; }

        public bool LhmOpenThrows { get; set; }

        public Func<FakeNvapiAdvancedBridge, FakeNvapiAdvancedBridge> Script { get; set; } = static b =>
        {
            b.Facts = FakeNvapiAdvancedBridge.FullFacts();
            b.Live = FakeNvapiAdvancedBridge.FullLive();
            return b;
        };

        public AdvancedHardwareReader Reader(bool elevated, bool? pawnIo)
            => new(
                () =>
                {
                    if (NvapiFactoryThrows)
                    {
                        throw new InvalidOperationException("the bridge is not there");
                    }

                    FakeNvapiAdvancedBridge advanced = Script(new FakeNvapiAdvancedBridge());
                    Bridges.Add(advanced);
                    return new NvapiAdvancedReader(new FakeNvapiBridge { InitResult = NvapiInit }, advanced);
                },
                groups =>
                {
                    GroupsAsked.Add(groups);
                    return new LhmAdvancedReader(new FakeLhmComputer(groups.HasFlag(LhmGroups.Cpu) ? FakeSensorTree.WholePc() : [FakeSensorTree.WholePc()[0]]) { OpenThrows = LhmOpenThrows });
                },
                elevated,
                pawnIo,
                Time,
                Notes.Add);
    }

    [Fact]
    public void AnUnelevatedAgentOnAnNvidiaPcReadsTheCardAndSaysWhyTheRestIsNotRead()
    {
        var rig = new Rig();
        using AdvancedHardwareReader reader = rig.Reader(elevated: false, pawnIo: true);

        reader.Open();
        AdvancedSnapshot snapshot = reader.Tick();

        snapshot.Sources.Should().Be(new AdvancedSources(AdvancedSourceStatus.Reading, NvapiCode: null, AdvancedSourceStatus.Reading, AdvancedSourceStatus.NotElevated));
        rig.GroupsAsked.Should().Equal(LhmGroups.Gpu);
        snapshot.TakenAt.Should().Be(rig.Time.GetUtcNow());
        snapshot.Readings.Should().Contain(static r => string.Equals(r.Series, "adv.nvapi.vram.available", StringComparison.Ordinal));
        snapshot.Readings.Should().Contain(static r => r.Series.StartsWith("adv.lhm.gpu.", StringComparison.Ordinal));
        snapshot.Readings.Should().NotContain(static r => r.Series.StartsWith("adv.lhm.cpu.", StringComparison.Ordinal) || r.Series.StartsWith("adv.lhm.board.", StringComparison.Ordinal));
        snapshot.Readings.Select(static r => r.Series).Should().OnlyHaveUniqueItems();
        snapshot.MachineFacts.Should().Contain(static f => string.Equals(f.Id, "nvapi.gpu.vbios_version", StringComparison.Ordinal));
        snapshot.MachineFacts.Should().NotContain(static f => f.Id.StartsWith("lhm.", StringComparison.Ordinal));
        snapshot.Series.Select(static s => s.Series).Should().BeSubsetOf(snapshot.Readings.Select(static r => r.Series));
        rig.Notes.Should().BeEmpty();
    }

    [Fact]
    public void NvapiIsReadEveryTickAndTheSensorLibraryEveryFifthWithNoValueRepeatedBetween()
    {
        // Measured before it was built (probe-advanced, 2026-11-11): 0.4 ms of processor time for NVAPI, 14 ms for one
        // refresh of the sensor library. Once a second the second alone is above NFR-2's 1% of one core.
        var rig = new Rig();
        using AdvancedHardwareReader reader = rig.Reader(elevated: false, pawnIo: false);
        reader.Open();

        AdvancedSnapshot[] ticks = [.. Enumerable.Range(0, 11).Select(_ => reader.Tick())];

        int[] withSensors = [.. Enumerable.Range(0, ticks.Length).Where(i => ticks[i].Readings.Any(static r => r.Series.StartsWith("adv.lhm.", StringComparison.Ordinal)))];
        withSensors.Should().Equal(0, 5, 10);
        ticks.Should().OnlyContain(static t => t.Readings.Any(static r => r.Series.StartsWith("adv.nvapi.", StringComparison.Ordinal)), "NVAPI is read on every tick");
        ticks[1].Readings.Should().NotContain(static r => r.Series.StartsWith("adv.lhm.", StringComparison.Ordinal), "a value that was not read on a tick is not that tick's value");
        ticks[1].Series.Should().BeEquivalentTo(ticks[0].Series, "what each series is, is known on every tick");
        ticks[1].Series.Should().NotBeEmpty();
    }

    [Fact]
    public void AnAdministratorWithoutPawnIoIsToldPawnIoIsMissingAndTheGroupsStayClosed()
    {
        var rig = new Rig();
        using AdvancedHardwareReader reader = rig.Reader(elevated: true, pawnIo: false);

        reader.Open();

        reader.Tick().Sources.ElevatedSensors.Should().Be(AdvancedSourceStatus.PawnIoMissing);
        rig.GroupsAsked.Should().Equal(LhmGroups.Gpu);
    }

    [Fact]
    public void AnAdministratorWithPawnIoReadsTheProcessorTheBoardAndTheMemoryAsWell()
    {
        var rig = new Rig();
        using AdvancedHardwareReader reader = rig.Reader(elevated: true, pawnIo: true);

        reader.Open();
        AdvancedSnapshot snapshot = reader.Tick();

        snapshot.Sources.ElevatedSensors.Should().Be(AdvancedSourceStatus.Reading);
        rig.GroupsAsked.Should().Equal(LhmGroups.Gpu | LhmGroups.Cpu | LhmGroups.Memory | LhmGroups.Motherboard);
        snapshot.Readings.Should().Contain(new AdvancedReading("adv.lhm.board.voltage.lpc_nct6798d_0_7", -11.97f));
        snapshot.Readings.Should().Contain(static r => r.Series.StartsWith("adv.lhm.cpu.temperature.", StringComparison.Ordinal));
        snapshot.MachineFacts.Should().Contain(static f => string.Equals(f.Id, "lhm.board.model", StringComparison.Ordinal));
        snapshot.Series.Should().Contain(static s => string.Equals(s.MetricId, "lhm.cpu.temperature", StringComparison.Ordinal) && s.Label.Contains("CPU Core #3", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(NativeNvapiBridge.NoGpu)]
    [InlineData(NativeNvapiBridge.Unavailable)]
    public void APcWithNoNvidiaCardSaysSoWithNvapisOwnStatusAndGoesOnReadingTheSensors(int init)
    {
        var rig = new Rig { NvapiInit = init };
        using AdvancedHardwareReader reader = rig.Reader(elevated: false, pawnIo: false);

        reader.Open();
        AdvancedSnapshot snapshot = reader.Tick();

        snapshot.Sources.Nvapi.Should().Be(AdvancedSourceStatus.NotPresent);
        snapshot.Sources.NvapiCode.Should().Be(init);
        snapshot.Readings.Should().NotContain(static r => r.Series.StartsWith("adv.nvapi.", StringComparison.Ordinal), "N/A, never a guess");
        snapshot.Readings.Should().Contain(static r => r.Series.StartsWith("adv.lhm.gpu.", StringComparison.Ordinal));
        snapshot.MachineFacts.Should().BeEmpty();
        reader.ReadSessionFacts(new AdvancedSessionTarget(4242, @"C:\Games\game.exe", @"\\.\DISPLAY1")).Should().BeEmpty();
        rig.Bridges.Single().Calls.Should().BeEmpty("a source that is not there is not asked");
    }

    [Fact]
    public void ABridgeThatCannotBeMadeAndALibraryThatDoesNotOpenAreStatesAndNeverExceptions()
    {
        var rig = new Rig { NvapiFactoryThrows = true, LhmOpenThrows = true };
        using AdvancedHardwareReader reader = rig.Reader(elevated: true, pawnIo: true);

        reader.Open();
        AdvancedSnapshot snapshot = reader.Tick();

        snapshot.Sources.Should().Be(new AdvancedSources(AdvancedSourceStatus.Faulted, NvapiCode: null, AdvancedSourceStatus.Faulted, AdvancedSourceStatus.Faulted));
        snapshot.Readings.Should().BeEmpty();
        snapshot.MachineFacts.Should().BeEmpty();
        rig.Notes.Should().HaveCount(2);
        rig.Notes.Should().Contain(static n => n.Contains("the bridge is not there", StringComparison.Ordinal));
        rig.Notes.Should().Contain(static n => n.Contains("the driver refused", StringComparison.Ordinal));
    }

    [Fact]
    public void ALibraryThatDoesNotOpenForAnUnelevatedAgentStillSaysNotElevatedForTheGroupsItNeverAsked()
    {
        var rig = new Rig { LhmOpenThrows = true };
        using AdvancedHardwareReader reader = rig.Reader(elevated: false, pawnIo: true);

        reader.Open();

        AdvancedSources sources = reader.Tick().Sources;
        sources.Sensors.Should().Be(AdvancedSourceStatus.Faulted);
        sources.ElevatedSensors.Should().Be(AdvancedSourceStatus.NotElevated, "they were never asked for, and that is why they are not read");
    }

    [Fact]
    public void ACardThatAnsweredAndThenAnswersNothingTwiceRunningIsInitialisedAgainOnce()
    {
        // 20_OPEN_QUESTIONS M12, the outcome written down before this was built.
        var rig = new Rig();
        using AdvancedHardwareReader reader = rig.Reader(elevated: false, pawnIo: false);
        reader.Open();
        reader.Tick().Readings.Should().Contain(static r => r.Series.StartsWith("adv.nvapi.", StringComparison.Ordinal));

        rig.Bridges[0].Live = new NvapiLive { Present = 0 };
        reader.Tick().Sources.Nvapi.Should().Be(AdvancedSourceStatus.Reading, "one empty tick is not two");
        rig.Bridges.Should().ContainSingle();
        AdvancedSnapshot second = reader.Tick();

        rig.Bridges.Should().HaveCount(2, "the bridge was released and made again");
        second.Sources.Nvapi.Should().Be(AdvancedSourceStatus.Reading);
        rig.Notes.Should().ContainSingle().Which.Should().Contain("initialising it again, once");
        reader.Tick().Readings.Should().Contain(static r => r.Series.StartsWith("adv.nvapi.", StringComparison.Ordinal), "the new bridge answers");
    }

    [Fact]
    public void IfThatDoesNotBringTheFieldsBackNvapiIsOffAndTheSensorsGoOn()
    {
        var rig = new Rig();
        using AdvancedHardwareReader reader = rig.Reader(elevated: false, pawnIo: false);
        reader.Open();
        reader.Tick();
        rig.Script = static b =>
        {
            b.Live = new NvapiLive { Present = 0 };
            return b;
        };
        rig.Bridges[0].Live = new NvapiLive { Present = 0 };

        reader.Tick();
        reader.Tick();
        rig.Bridges.Should().HaveCount(2);
        reader.Tick();
        AdvancedSnapshot last = reader.Tick();

        last.Sources.Nvapi.Should().Be(AdvancedSourceStatus.Faulted);
        rig.Bridges.Should().HaveCount(2, "initialised again once, not in a loop");
        rig.Notes.Should().HaveCount(2);
        rig.Notes[1].Should().Contain("off until the reader is opened again");
        last.Sources.Sensors.Should().Be(AdvancedSourceStatus.Reading);
        reader.Tick().Readings.Should().Contain(static r => r.Series.StartsWith("adv.lhm.gpu.", StringComparison.Ordinal), "the sixth tick is the sensors' next");
        reader.ReadSessionFacts(new AdvancedSessionTarget(1, @"C:\g.exe", GdiDeviceName: null)).Should().BeEmpty();
    }

    [Fact]
    public void ACardThatNeverAnswersALiveFieldIsACardWithoutThoseSensorsAndNotAFault()
    {
        var rig = new Rig
        {
            Script = static b =>
            {
                b.Facts = FakeNvapiAdvancedBridge.FullFacts();
                b.Live = new NvapiLive { Present = 0 };
                return b;
            },
        };
        using AdvancedHardwareReader reader = rig.Reader(elevated: false, pawnIo: false);
        reader.Open();

        for (int i = 0; i < 6; i++)
        {
            reader.Tick();
        }

        reader.Tick().Sources.Nvapi.Should().Be(AdvancedSourceStatus.Reading);
        rig.Bridges.Should().ContainSingle();
        rig.Notes.Should().BeEmpty();
    }

    [Fact]
    public void TheFactsOfThePcAreReadOnceWhenTheReaderOpens()
    {
        var rig = new Rig();
        using AdvancedHardwareReader reader = rig.Reader(elevated: false, pawnIo: false);

        reader.Open();
        reader.Open();
        reader.Tick();
        reader.Tick();

        rig.Bridges.Single().Calls.Count(static c => string.Equals(c, "facts", StringComparison.Ordinal)).Should().Be(1);
        rig.Bridges.Single().Calls.Count(static c => string.Equals(c, "live", StringComparison.Ordinal)).Should().Be(2);
        rig.GroupsAsked.Should().ContainSingle();
    }

    [Fact]
    public void ASessionsFactsAreAskedOfNvapiForThatProcessThatExecutableAndThatDisplay()
    {
        var rig = new Rig
        {
            Script = static b =>
            {
                b.Session = new NvapiSessionDisplay { NvapiStatus = 0, Present = NvapiSessionDisplay.HasColorSpace, ColorSpace = 12 };
                return b;
            },
        };
        using AdvancedHardwareReader reader = rig.Reader(elevated: false, pawnIo: false);
        reader.Open();

        IReadOnlyList<AdvancedFact> facts = reader.ReadSessionFacts(new AdvancedSessionTarget(4242, @"C:\Games\game.exe", @"\\.\DISPLAY2"));

        facts.Should().ContainSingle().Which.Id.Should().Be("nvapi.session.source_colorspace");
        rig.Bridges.Single().Calls.Should().Contain(@"session-display \\.\DISPLAY2 4242");
        rig.Bridges.Single().Calls.Should().Contain(@"profile C:\Games\game.exe own=True");
        FluentActions.Invoking(() => reader.ReadSessionFacts(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BeforeOpeningAndAfterDisposingATickCarriesNothing()
    {
        var rig = new Rig();
        AdvancedHardwareReader reader = rig.Reader(elevated: false, pawnIo: false);

        AdvancedSnapshot before = reader.Tick();
        reader.Open();
        reader.Dispose();
        reader.Dispose();
        AdvancedSnapshot after = reader.Tick();

        before.Readings.Should().BeEmpty();
        before.Sources.Should().Be(AdvancedSources.Unknown);
        after.Readings.Should().BeEmpty();
        reader.ReadSessionFacts(new AdvancedSessionTarget(1, ExecutablePath: null, GdiDeviceName: null)).Should().BeEmpty();
        FluentActions.Invoking(reader.Open).Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void TheMakersAndTheClockAreRequired()
    {
        TimeProvider time = TimeProvider.System;

        FluentActions.Invoking(() => new AdvancedHardwareReader(null!, static _ => new LhmAdvancedReader(new FakeLhmComputer()), false, false, time)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AdvancedHardwareReader(NvapiAdvancedReader.OverTheNativeBridge, null!, false, false, time)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AdvancedHardwareReader(NvapiAdvancedReader.OverTheNativeBridge, static _ => new LhmAdvancedReader(new FakeLhmComputer()), false, false, null!)).Should().Throw<ArgumentNullException>();
    }
}
