// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Application.Advanced;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>
/// "This PC, now" for the Advanced page (owner decisions D101 and D107): asked only while watched, the last value of
/// every series kept with its age, the facts asked for once, and every way the Agent can fail to answer named.
/// </summary>
public sealed class AdvancedMachineFeedTests
{
    private const string _vram = "adv.nvapi.vram.in_use";
    private const string _fan = "adv.lhm.gpu.fan.gpu-nvidia_0_1";

    private static readonly DateTimeOffset _t0 = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void BeforeAnyoneWatchesNothingIsAskedAndNothingIsKnown()
    {
        var agent = new FakeAgentLink();
        using var feed = new AdvancedMachineFeed(agent, TimeProvider.System);

        feed.Current.State.Should().Be(AdvancedFeedState.Stopped);
        feed.Current.Values.Should().BeEmpty();
        agent.Sent.Should().BeEmpty("D107: with no page open the App asks nothing, so the Agent's reader is not held for it");
    }

    [Fact]
    public async Task ASnapshotBecomesTheViewAndTheFactsAreAskedForOnce()
    {
        var agent = new FakeAgentLink { Answer = static (_, _) => Ack(Tick(0, withFacts: true, (_vram, 5120), (_fan, 1400))) };
        using var feed = new AdvancedMachineFeed(agent, TimeProvider.System);

        await feed.PollOnceAsync(Ct);

        AdvancedMachineView view = feed.Current;
        view.State.Should().Be(AdvancedFeedState.Live);
        view.TakenAt.Should().Be(_t0);
        view.Facts.Should().ContainSingle().Which.Id.Should().Be("nvapi.gpu.rt_cores");
        view.Values[_vram].Should().Be(new AdvancedLivePoint(_t0, 5120));
        view.Series.Should().ContainSingle().Which.Label.Should().Be("GPU Fan 1");
        view.Sources.Should().Be(new AdvancedSources(AdvancedSourceStatus.Reading, NvapiCode: null, AdvancedSourceStatus.Reading, AdvancedSourceStatus.NotElevated));
        agent.Sent.Single().Payload.Should().Be(new GetAdvancedMachineRequest(null), "the first request holds no facts");

        agent.Answer = static (_, _) => Ack(Tick(1, withFacts: false, (_vram, 5200)));
        await feed.PollOnceAsync(Ct);

        agent.Sent[1].Payload.Should().Be(new GetAdvancedMachineRequest("hash-1"), "and every later one names the facts it holds, so they are not sent again");
        feed.Current.Facts.Should().ContainSingle("an answer that leaves the facts out leaves the kept ones in place");
    }

    [Fact]
    public async Task ASeriesNotReadOnATickKeepsItsLastValueAndItsAge()
    {
        // The sensors are read every fifth tick: between two readings the fan is still "1400, as of then".
        var agent = new FakeAgentLink { Answer = static (_, _) => Ack(Tick(0, withFacts: true, (_vram, 5120), (_fan, 1400))) };
        using var feed = new AdvancedMachineFeed(agent, TimeProvider.System);
        await feed.PollOnceAsync(Ct);

        agent.Answer = static (_, _) => Ack(Tick(1, withFacts: false, (_vram, 5200)));
        await feed.PollOnceAsync(Ct);

        feed.Current.Values[_vram].Should().Be(new AdvancedLivePoint(_t0.AddSeconds(1), 5200));
        feed.Current.Values[_fan].Should().Be(new AdvancedLivePoint(_t0, 1400), "the page shows the value with the tick it is of, never as if it were new");
        feed.HistoryOf(_vram).Should().HaveCount(2);
        feed.HistoryOf(_fan).Should().ContainSingle();
        feed.HistoryOf("adv.never.read").Should().BeEmpty();
    }

    [Fact]
    public async Task TheSameTickAnsweredTwiceAddsNoPoint()
    {
        var agent = new FakeAgentLink { Answer = static (_, _) => Ack(Tick(0, withFacts: true, (_vram, 5120))) };
        using var feed = new AdvancedMachineFeed(agent, TimeProvider.System);

        await feed.PollOnceAsync(Ct);
        await feed.PollOnceAsync(Ct);

        feed.HistoryOf(_vram).Should().ContainSingle("the reader's second and this one are not in step; one tick is one point");
    }

    [Fact]
    public async Task TheHistoryOfASeriesIsBounded()
    {
        int tick = 0;
        var agent = new FakeAgentLink { Answer = (_, _) => Ack(Tick(tick++, withFacts: false, (_vram, tick))) };
        using var feed = new AdvancedMachineFeed(agent, TimeProvider.System);

        for (int i = 0; i < AdvancedMachineFeed.HistoryPoints + 5; i++)
        {
            await feed.PollOnceAsync(Ct);
        }

        IReadOnlyList<AdvancedLivePoint> history = feed.HistoryOf(_vram);
        history.Should().HaveCount(AdvancedMachineFeed.HistoryPoints);
        history[0].At.Should().Be(_t0.AddSeconds(5), "the oldest points go first");
    }

    [Fact]
    public async Task AReaderThatHasNotReadYetIsWaitingNotEmpty()
    {
        var agent = new FakeAgentLink
        {
            Answer = static (_, _) => Ack(new AdvancedMachineAck(Ready: false, 0, null, null, [], [], "Unknown", null, "Unknown", "Unknown")),
        };
        using var feed = new AdvancedMachineFeed(agent, TimeProvider.System);

        await feed.PollOnceAsync(Ct);

        feed.Current.State.Should().Be(AdvancedFeedState.Waiting);
        feed.Current.TakenAt.Should().BeNull();
    }

    [Fact]
    public async Task OffForgetsWhatWasShown()
    {
        var agent = new FakeAgentLink { Answer = static (_, _) => Ack(Tick(0, withFacts: true, (_vram, 5120))) };
        using var feed = new AdvancedMachineFeed(agent, TimeProvider.System);
        await feed.PollOnceAsync(Ct);

        agent.Answer = static (_, _) => throw new IpcRequestException(IpcErrorCode.AdvancedOff, "off");
        await feed.PollOnceAsync(Ct);

        feed.Current.State.Should().Be(AdvancedFeedState.Off);
        feed.Current.Values.Should().BeEmpty("off means nothing is read: a value from before is not 'now'");
        feed.Current.Facts.Should().BeEmpty();
        feed.HistoryOf(_vram).Should().BeEmpty();

        agent.Answer = static (_, _) => Ack(Tick(9, withFacts: true, (_vram, 6000)));
        await feed.PollOnceAsync(Ct);

        agent.Sent[^1].Payload.Should().Be(new GetAdvancedMachineRequest(null), "and switched on again it asks for the facts afresh");
        feed.Current.State.Should().Be(AdvancedFeedState.Live);
    }

    [Fact]
    public async Task AnOlderAgentAndNoAgentAreSaidAndWhatWasShownStays()
    {
        var agent = new FakeAgentLink { Answer = static (_, _) => Ack(Tick(0, withFacts: true, (_vram, 5120))) };
        using var feed = new AdvancedMachineFeed(agent, TimeProvider.System);
        await feed.PollOnceAsync(Ct);

        agent.Answer = static (_, _) => throw new InvalidOperationException("not connected");
        await feed.PollOnceAsync(Ct);
        feed.Current.State.Should().Be(AdvancedFeedState.NoAgent);
        feed.Current.Values[_vram].Value.Should().Be(5120, "the Agent restarting does not blank the page; the values keep their age");

        agent.Answer = static (_, _) => throw new TimeoutException("no answer");
        await feed.PollOnceAsync(Ct);
        feed.Current.State.Should().Be(AdvancedFeedState.NoAgent);

        agent.Answer = static (_, _) => throw new IpcRequestException(IpcErrorCode.UnknownType, "unknown message type GetAdvancedMachine");
        await feed.PollOnceAsync(Ct);
        feed.Current.State.Should().Be(AdvancedFeedState.OlderAgent);

        agent.Answer = static (_, _) => throw new IpcRequestException(IpcErrorCode.HandlerFaulted, "boom");
        await feed.PollOnceAsync(Ct);
        feed.Current.State.Should().Be(AdvancedFeedState.NoAgent, "any other error is no answer, and the next second asks again");
    }

    [Fact]
    public async Task ASourceStateThisBuildDoesNotKnowIsUnknownNeverAGuess()
    {
        AdvancedMachineAck ack = Tick(0, withFacts: true, (_vram, 1)) with { Nvapi = "SomethingNewer", Sensors = "7" };
        var agent = new FakeAgentLink { Answer = (_, _) => Ack(ack) };
        using var feed = new AdvancedMachineFeed(agent, TimeProvider.System);

        await feed.PollOnceAsync(Ct);

        feed.Current.Sources.Nvapi.Should().Be(AdvancedSourceStatus.Unknown);
        feed.Current.Sources.Sensors.Should().Be(AdvancedSourceStatus.Unknown, "a number is not a name");
    }

    [Fact]
    public async Task WatchingAsksUntilTheLastWatcherLetsGoThenSaysStopped()
    {
        int asked = 0;
        var agent = new FakeAgentLink
        {
            Answer = (_, _) =>
            {
                Interlocked.Increment(ref asked);
                return Ack(Tick(0, withFacts: true, (_vram, 5120)));
            },
        };
        using var feed = new AdvancedMachineFeed(agent, TimeProvider.System, TimeSpan.FromMilliseconds(20));
        int changes = 0;
        feed.Changed += (_, _) => Interlocked.Increment(ref changes);

        IDisposable one = feed.Watch();
        IDisposable two = feed.Watch();
        await UntilAsync(() => Volatile.Read(ref asked) >= 1);
        one.Dispose();
        one.Dispose();
        int before = Volatile.Read(ref asked);
        await UntilAsync(() => Volatile.Read(ref asked) >= before + 2);
        feed.Current.State.Should().Be(AdvancedFeedState.Live, "a second watcher still holds it, and a handle disposed twice counts once");

        two.Dispose();

        feed.Current.State.Should().Be(AdvancedFeedState.Stopped);
        Volatile.Read(ref changes).Should().BeGreaterThan(0);
        await Task.Delay(100, Ct);
        int after = Volatile.Read(ref asked);
        await Task.Delay(200, Ct);
        Volatile.Read(ref asked).Should().Be(after, "D107: nobody watches, so nothing is asked and the Agent lets its reader go");
        feed.Current.State.Should().Be(AdvancedFeedState.Stopped, "and a request that was in flight does not speak after 'stopped'");
    }

    private static async Task UntilAsync(Func<bool> met)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (!met())
        {
            DateTime.UtcNow.Should().BeBefore(deadline, "the feed asks every 20 ms while watched");
            await Task.Delay(10, Ct).ConfigureAwait(false);
        }
    }

    private static IpcEnvelope Ack(AdvancedMachineAck ack) => FakeAgentLink.Envelope(IpcMessageType.AdvancedMachineAck, ack);

    private static AdvancedMachineAck Tick(int second, bool withFacts, params (string Series, double Value)[] readings) => new(
        Ready: true,
        _t0.AddSeconds(second).ToUnixTimeMilliseconds(),
        "hash-1",
        withFacts ? AdvancedJson.OfFacts([new AdvancedFact("nvapi.gpu.rt_cores", Instance: null, AdvancedValue.Of(84))]) : null,
        [.. readings.Select(static r => new AdvancedReadingValue(r.Series, r.Value))],
        [new AdvancedSeriesLabel(_fan, "lhm.gpu.fan", "GPU Fan 1")],
        nameof(AdvancedSourceStatus.Reading),
        NvapiCode: null,
        nameof(AdvancedSourceStatus.Reading),
        nameof(AdvancedSourceStatus.NotElevated));
}
