// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.Ipc;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Tests.Ipc;

/// <summary>
/// <c>GetAdvancedMachine</c> (owner decisions D101 and D107): the answer is what the shared reader last read and
/// nothing else, a request is the page's hold on that reader for a lease, and off means nothing is held or answered.
/// </summary>
public sealed class AdvancedMachineResponderTests
{
    private static readonly TimeSpan _lease = TimeSpan.FromSeconds(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WithTheOptionOffTheAnswerIsAdvancedOffAndTheReaderIsNeverHeld()
    {
        var hub = new FakeHub { Latest = Snapshot() };
        var clock = new ManualClock();
        using var responder = new AdvancedMachineResponder(hub, static _ => ValueTask.FromResult(false), clock, _lease);

        IpcEnvelope answer = await AskAsync(responder, hash: null);

        answer.Type.Should().Be(IpcMessageType.Error);
        IpcCodec.Payload<ErrorAck>(answer).Should().BeOfType<ErrorAck>().Which.Code.Should().Be(IpcErrorCode.AdvancedOff);
        hub.Acquired.Should().Be(0, "D101: off means nothing is read, the page included");
        responder.IsHolding.Should().BeFalse();
    }

    [Fact]
    public async Task TheFirstRequestHoldsTheReaderAndSaysItHasNotReadYet()
    {
        var hub = new FakeHub { Latest = null };
        using var responder = new AdvancedMachineResponder(hub, static _ => ValueTask.FromResult(true), new ManualClock(), _lease);

        AdvancedMachineAck ack = Ack(await AskAsync(responder, hash: null));

        hub.Acquired.Should().Be(1);
        ack.Ready.Should().BeFalse("the reader opens its sources on a thread of its own; the page shows that it is being read");
        ack.Readings.Should().BeEmpty();
        ack.FactsJson.Should().BeNull();
        ack.Nvapi.Should().Be(nameof(AdvancedSourceStatus.Unknown));
    }

    [Fact]
    public async Task ASnapshotIsAnsweredAsItWasReadWithItsSourcesByName()
    {
        var hub = new FakeHub { Latest = Snapshot() };
        using var responder = new AdvancedMachineResponder(hub, static _ => ValueTask.FromResult(true), new ManualClock(), _lease);

        AdvancedMachineAck ack = Ack(await AskAsync(responder, hash: null));

        ack.Ready.Should().BeTrue();
        ack.TakenAtUnixMs.Should().Be(Snapshot().TakenAt.ToUnixTimeMilliseconds());
        ack.Readings.Should().Equal(new AdvancedReadingValue("adv.nvapi.vram.in_use", 5120));
        ack.Series.Should().Equal(new AdvancedSeriesLabel("adv.lhm.gpu.fan.gpu-nvidia_0_1", "lhm.gpu.fan", "GPU Fan 1"));
        ack.Nvapi.Should().Be(nameof(AdvancedSourceStatus.Reading));
        ack.Sensors.Should().Be(nameof(AdvancedSourceStatus.Reading));
        ack.ElevatedSensors.Should().Be(nameof(AdvancedSourceStatus.NotElevated), "why a source answers nothing is part of the answer");
        AdvancedJson.ParseFacts(ack.FactsJson).Should().ContainSingle().Which.Id.Should().Be("nvapi.gpu.rt_cores");
        ack.FactsHash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task AReadingThatIsNotANumberIsLeftOutBecauseTheWireHasNoWordForIt()
    {
        AdvancedSnapshot snapshot = Snapshot() with
        {
            Readings = [new AdvancedReading("adv.nvapi.vram.in_use", double.NaN), new AdvancedReading("adv.nvapi.pstate", 0)],
        };
        var hub = new FakeHub { Latest = snapshot };
        using var responder = new AdvancedMachineResponder(hub, static _ => ValueTask.FromResult(true), new ManualClock(), _lease);

        AdvancedMachineAck ack = Ack(await AskAsync(responder, hash: null));

        ack.Readings.Should().Equal(new AdvancedReadingValue("adv.nvapi.pstate", 0));
    }

    [Fact]
    public async Task TheFactsAreSentOnceAndLeftOutWhileTheClientNamesTheirHash()
    {
        var hub = new FakeHub { Latest = Snapshot() };
        using var responder = new AdvancedMachineResponder(hub, static _ => ValueTask.FromResult(true), new ManualClock(), _lease);
        AdvancedMachineAck first = Ack(await AskAsync(responder, hash: null));

        AdvancedMachineAck second = Ack(await AskAsync(responder, first.FactsHash));

        second.FactsJson.Should().BeNull("the facts change when the hardware does, not every second");
        second.FactsHash.Should().Be(first.FactsHash);
        second.Readings.Should().NotBeEmpty("and everything else is answered as before");

        AdvancedMachineAck stale = Ack(await AskAsync(responder, "not-the-hash"));
        stale.FactsJson.Should().Be(first.FactsJson, "a client that holds other facts gets these");
    }

    [Fact]
    public async Task RequestsRenewOneHoldAndTheReaderIsLetGoALeaseAfterTheLast()
    {
        var hub = new FakeHub { Latest = Snapshot() };
        var clock = new ManualClock();
        using var responder = new AdvancedMachineResponder(hub, static _ => ValueTask.FromResult(true), clock, _lease);

        await AskAsync(responder, hash: null);
        clock.Advance(TimeSpan.FromSeconds(4));
        await AskAsync(responder, hash: null);
        clock.Advance(TimeSpan.FromSeconds(4));
        responder.ReleaseIfExpired();

        hub.Acquired.Should().Be(1, "a page open for a minute holds the reader once");
        hub.Released.Should().Be(0, "eight seconds after the first request and four after the last: the lease still runs");
        clock.LastDue.Should().Be(TimeSpan.FromSeconds(1), "and the timer waits for what is left of it");

        clock.Advance(TimeSpan.FromSeconds(1));
        responder.ReleaseIfExpired();

        hub.Released.Should().Be(1, "D107: the page closed - nothing asked for a lease - so the reader is let go");
        responder.IsHolding.Should().BeFalse();

        await AskAsync(responder, hash: null);
        hub.Acquired.Should().Be(2, "and the page opening again holds it again");
    }

    [Fact]
    public async Task SwitchingTheOptionOffLetsGoOfAHoldAtTheNextRequest()
    {
        bool on = true;
        var hub = new FakeHub { Latest = Snapshot() };
        var clock = new ManualClock();
        using var responder = new AdvancedMachineResponder(hub, _ => ValueTask.FromResult(on), clock, _lease);
        await AskAsync(responder, hash: null);

        on = false;
        clock.Advance(TimeSpan.FromSeconds(1));
        IpcEnvelope cached = await AskAsync(responder, hash: null);
        cached.Type.Should().Be(IpcMessageType.AdvancedMachineAck, "the option is asked for at most every two seconds");

        clock.Advance(TimeSpan.FromSeconds(2));
        IpcEnvelope answer = await AskAsync(responder, hash: null);

        answer.Type.Should().Be(IpcMessageType.Error);
        hub.Released.Should().Be(1, "off means nothing is read: the hold goes at once, not a lease later");
    }

    [Fact]
    public async Task TheOptionIsReadAtMostOnceInTwoSeconds()
    {
        int asked = 0;
        var clock = new ManualClock();
        using var responder = new AdvancedMachineResponder(new FakeHub(), _ =>
        {
            asked++;
            return ValueTask.FromResult(true);
        }, clock, _lease);

        await AskAsync(responder, hash: null);
        clock.Advance(TimeSpan.FromSeconds(1));
        await AskAsync(responder, hash: null);
        asked.Should().Be(1, "the pipe's read loop awaits this handler; one settings row every two seconds is all it reads");

        clock.Advance(TimeSpan.FromSeconds(1));
        await AskAsync(responder, hash: null);
        asked.Should().Be(2);
    }

    [Fact]
    public async Task DisposingLetsGoOfTheHold()
    {
        var hub = new FakeHub();
        var responder = new AdvancedMachineResponder(hub, static _ => ValueTask.FromResult(true), new ManualClock(), _lease);
        await AskAsync(responder, hash: null);

        responder.Dispose();
        responder.Dispose();

        hub.Released.Should().Be(1);
    }

    [Fact]
    public async Task TheRequestHandlerAnswersItOnlyWhereAResponderIsComposed()
    {
        var identity = new AgentIdentity("1.0.0", 1, Elevated: false, "build", VulkanLayerRegistered: false, CpuTempAvailable: false, "d", "e");
        using var responder = new AdvancedMachineResponder(new FakeHub { Latest = Snapshot() }, static _ => ValueTask.FromResult(true), new ManualClock(), _lease);
        var with = new AgentRequestHandler(identity, static () => "l1", static () => AgentStatus.Idle, advanced: responder);
        var without = new AgentRequestHandler(identity, static () => "l1", static () => AgentStatus.Idle);
        IpcEnvelope request = IpcCodec.Decode(IpcCodec.Encode(IpcMessageType.GetAdvancedMachine, "7", new GetAdvancedMachineRequest()));

        IpcEnvelope answered = IpcCodec.Decode(await with.HandleAsync(request, Ct));
        IpcEnvelope unknown = IpcCodec.Decode(await without.HandleAsync(request, Ct));

        answered.Type.Should().Be(IpcMessageType.AdvancedMachineAck);
        answered.Id.Should().Be("7");
        unknown.Type.Should().Be(IpcMessageType.Error);
        IpcCodec.Payload<ErrorAck>(unknown).Should().BeOfType<ErrorAck>().Which.Code.Should().Be(IpcErrorCode.UnknownType,
            "which is what an App of this version gets from an older Agent, and shows as such");
    }

    private static async Task<IpcEnvelope> AskAsync(AdvancedMachineResponder responder, string? hash)
    {
        IpcEnvelope request = IpcCodec.Decode(IpcCodec.Encode(IpcMessageType.GetAdvancedMachine, "1", new GetAdvancedMachineRequest(hash)));
        return IpcCodec.Decode(await responder.AnswerAsync(request, Ct).ConfigureAwait(false));
    }

    private static AdvancedMachineAck Ack(IpcEnvelope envelope)
    {
        envelope.Type.Should().Be(IpcMessageType.AdvancedMachineAck);
        return IpcCodec.Payload<AdvancedMachineAck>(envelope).Should().BeOfType<AdvancedMachineAck>().Subject;
    }

    private static AdvancedSnapshot Snapshot() => new(
        new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero),
        [new AdvancedReading("adv.nvapi.vram.in_use", 5120)],
        _facts,
        [new AdvancedSeriesInfo("adv.lhm.gpu.fan.gpu-nvidia_0_1", "lhm.gpu.fan", "GPU Fan 1")],
        new AdvancedSources(AdvancedSourceStatus.Reading, NvapiCode: null, AdvancedSourceStatus.Reading, AdvancedSourceStatus.NotElevated));

    private static readonly IReadOnlyList<AdvancedFact> _facts = [new AdvancedFact("nvapi.gpu.rt_cores", Instance: null, AdvancedValue.Of(84))];

    /// <summary>The Agent's shared reader, counted: how often it was held and let go.</summary>
    private sealed class FakeHub : IAdvancedHardware
    {
        public int Acquired { get; private set; }

        public int Released { get; private set; }

        public AdvancedSnapshot? Latest { get; init; }

        public IDisposable Acquire()
        {
            Acquired++;
            return new Hold(this);
        }

        public IReadOnlyList<AdvancedFact> ReadSessionFacts(AdvancedSessionTarget target) => [];

        private sealed class Hold(FakeHub hub) : IDisposable
        {
            public void Dispose() => hub.Released++;
        }
    }

    /// <summary>A clock the test moves, whose one timer never fires by itself: the test runs the callback's work.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _ticks;

        /// <summary>What the timer was last set to wait.</summary>
        public TimeSpan LastDue { get; private set; }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new Timer(this);

        private sealed class Timer(ManualClock clock) : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                clock.LastDue = dueTime;
                return true;
            }

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
