// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.Telemetry;
using FrameLedger.Infrastructure.Telemetry;

namespace FrameLedger.Infrastructure.Tests.Telemetry;

/// <summary>
/// The poller and the shared advanced reader (owner decision D101): it holds the reader while it runs, attaches each
/// of the reader's ticks to one sample and to no other, and with no reader composed a sample is what it always was.
/// </summary>
public sealed class TelemetryPollerAdvancedTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private sealed class Clock : TimeProvider
    {
        public long Ticks { get; set; } = 1_000_000;

        public override DateTimeOffset GetUtcNow() => _t0;

        public override long GetTimestamp() => Ticks;
    }

    private sealed class FakeHub : IAdvancedHardware
    {
        public int Acquired { get; private set; }

        public int Released { get; private set; }

        public AdvancedSnapshot? Latest { get; set; }

        public IDisposable Acquire()
        {
            Acquired++;
            return new Hold(this);
        }

        public IReadOnlyList<AdvancedFact> ReadSessionFacts(AdvancedSessionTarget target) => [];

        public void Publish(params AdvancedReading[] readings)
            => Latest = new AdvancedSnapshot(_t0, readings, [], [], AdvancedSources.Unknown);

        private sealed class Hold(FakeHub hub) : IDisposable
        {
            public void Dispose() => hub.Released++;
        }
    }

    private static List<TelemetrySample> Drained(TelemetryPoller poller)
    {
        var samples = new List<TelemetrySample>();
        poller.Drain(samples);
        return samples;
    }

    [Fact]
    public void EachTickOfTheReaderIsAttachedToOneSampleAndIsNotRepeatedOnTheNext()
    {
        using var layer = new FakeLayer(TelemetryLayer.Lhm);
        layer.Publish(layer.Sample(_t0, load: 50));
        var hub = new FakeHub();
        using var poller = new TelemetryPoller(layer, new TelemetryPollerOptions(), new Clock(), advanced: hub);

        hub.Publish(new AdvancedReading("adv.nvapi.pstate", 0), new AdvancedReading("adv.lhm.board.voltage.lpc_0_7", -11.97));
        poller.PollOnce();
        poller.PollOnce();
        hub.Publish(new AdvancedReading("adv.nvapi.pstate", 8));
        poller.PollOnce();

        List<TelemetrySample> samples = Drained(poller);
        samples.Should().HaveCount(3);
        samples[0].Advanced.Should().Equal(new AdvancedReading("adv.nvapi.pstate", 0), new AdvancedReading("adv.lhm.board.voltage.lpc_0_7", -11.97));
        samples[1].Advanced.Should().BeNull("nothing newer arrived: a reading is of one tick, and the last one's is not repeated");
        samples[2].Advanced.Should().Equal(new AdvancedReading("adv.nvapi.pstate", 8));
        samples.Should().OnlyContain(static s => s.Sample.LoadPct == 50, "the fixed fields are untouched");
    }

    [Fact]
    public void ATickTheReaderPublishedWithNoReadingAttachesNothing()
    {
        using var layer = new FakeLayer(TelemetryLayer.Lhm);
        layer.Publish(layer.Sample(_t0, load: 50));
        var hub = new FakeHub();
        using var poller = new TelemetryPoller(layer, new TelemetryPollerOptions(), new Clock(), advanced: hub);

        hub.Publish();
        poller.PollOnce();

        Drained(poller).Should().ContainSingle().Which.Advanced.Should().BeNull();
    }

    [Fact]
    public void ATickOnlyTheAdvancedReaderAnsweredIsStillASample()
    {
        using var layer = new FakeLayer(TelemetryLayer.Lhm);
        var hub = new FakeHub();
        using var poller = new TelemetryPoller(layer, new TelemetryPollerOptions(), new Clock { Ticks = 77_000 }, advanced: hub);

        poller.PollOnce();
        Drained(poller).Should().BeEmpty("nothing answered at all");

        hub.Publish(new AdvancedReading("adv.nvapi.pstate", 0));
        poller.PollOnce();

        TelemetrySample only = Drained(poller).Should().ContainSingle().Subject;
        only.QpcTicks.Should().Be(77_000);
        only.Sample.Layer.Should().Be(TelemetryLayer.None, "the placeholder names no layer and no field");
        only.Advanced.Should().ContainSingle();
    }

    [Fact]
    public void WithNoReaderComposedASampleIsWhatItAlwaysWas()
    {
        using var layer = new FakeLayer(TelemetryLayer.Lhm);
        layer.Publish(layer.Sample(_t0, load: 50));
        using var poller = new TelemetryPoller(layer, new TelemetryPollerOptions(), new Clock());

        poller.PollOnce();

        Drained(poller).Should().ContainSingle().Which.Advanced.Should().BeNull();
    }

    [Fact]
    public void TheReaderIsHeldFromStartToDisposeAndNeverOwned()
    {
        using var layer = new FakeLayer(TelemetryLayer.Lhm);
        var hub = new FakeHub();
        var poller = new TelemetryPoller(layer, new TelemetryPollerOptions(), new Clock(), ownsSource: false, advanced: hub);

        hub.Acquired.Should().Be(0, "a poller that was never started holds nothing");
        poller.Start();
        poller.Start();
        hub.Acquired.Should().Be(1);
        hub.Released.Should().Be(0);

        poller.Dispose();
        poller.Dispose();

        hub.Released.Should().Be(1, "some seconds after the last holder lets go the shared reader closes (D107)");
    }

    [Fact]
    public void APollerThatWasNeverStartedReleasesNothing()
    {
        using var layer = new FakeLayer(TelemetryLayer.Lhm);
        var hub = new FakeHub();
        var poller = new TelemetryPoller(layer, new TelemetryPollerOptions(), new Clock(), advanced: hub);

        poller.Dispose();

        hub.Acquired.Should().Be(0);
        hub.Released.Should().Be(0);
    }
}
