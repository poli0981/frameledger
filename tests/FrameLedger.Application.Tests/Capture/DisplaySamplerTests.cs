// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;
using FluentAssertions;
using FrameLedger.Application.Capture;
using FrameLedger.Domain.Display;
using FrameLedger.Shared;

namespace FrameLedger.Application.Tests.Capture;

/// <summary>
/// The display mode's sampling (beta.10, <c>03_METRICS</c> §Display mode): a sample stands for the time until the next, a
/// pause is not time, and a stale swap-chain sample says nothing about exclusivity — on a clock the test moves.
/// </summary>
public sealed class DisplaySamplerTests
{
    private static readonly ScreenRect _monitor = new(0, 0, 1920, 1080);

    private static readonly WindowView _covering = new(false, _monitor, _monitor, 144);

    private static readonly WindowView _windowed = new(false, new ScreenRect(100, 100, 1380, 820), _monitor, 144);

    private static readonly int[] _pid = [4242];

    private sealed class Clock
    {
        public long Now { get; set; } = 1_000_000;

        public void Advance(TimeSpan by) => Now += (long)(by.TotalSeconds * Stopwatch.Frequency);
    }

    private sealed class SwitchableProbe : IDisplayProbe
    {
        public WindowView? Answer { get; set; }

        public List<ulong> Hwnds { get; } = [];

        public WindowView? Observe(IReadOnlyCollection<int> pids, ulong hwnd)
        {
            Hwnds.Add(hwnd);
            return Answer;
        }
    }

    private static FlDisplayState Region(long sampleQpc, bool exclusive, ulong hwnd = 0x42) => new()
    {
        Flags = FlDisplayFlags.Sampled | FlDisplayFlags.ExclusiveKnown | (exclusive ? FlDisplayFlags.Exclusive : FlDisplayFlags.None),
        Hwnd = hwnd,
        BufferWidth = 1920,
        BufferHeight = 1080,
        SwapEffect = 5, // FLIP_DISCARD (4), stored plus one
        SampleQpc = (ulong)sampleQpc,
    };

    [Fact]
    public void EachSampleStandsForTheTimeUntilTheNextAndTheLastIsClosedByFinish()
    {
        var clock = new Clock();
        var probe = new SwitchableProbe { Answer = _covering };
        var sampler = new DisplaySampler(probe, () => clock.Now);

        sampler.Sample(_pid, Region(clock.Now, exclusive: true), paused: false);
        clock.Advance(TimeSpan.FromMilliseconds(300));
        sampler.Sample(_pid, Region(clock.Now, exclusive: false), paused: false);
        clock.Advance(TimeSpan.FromMilliseconds(700));
        sampler.Finish(paused: false);

        DisplaySummary summary = sampler.Summary()!;
        summary.ExclusiveMs.Should().Be(300);
        summary.BorderlessMs.Should().Be(700);
        summary.Changes.Should().Be(1);
        summary.Dominant.Should().Be(DisplayMode.Borderless);
        summary.SharePercent(DisplayMode.ExclusiveFullscreen).Should().BeApproximately(30, 0.001);
        summary.SwapEffect.Should().Be("flip_discard");
        summary.Source.Should().Be(DisplaySource.SwapChain);
        probe.Hwnds.Should().Equal(0x42ul, 0x42ul);
    }

    /// <summary>
    /// The loop reads the pause at each tick, so an interval that ends on a paused tick is dropped: the pause is excluded to
    /// within one tick at each edge — 100 ms hooked — the way the frame series' gap is.
    /// </summary>
    [Fact]
    public void AnIntervalThatEndsOnAPausedTickIsNotCounted()
    {
        var clock = new Clock();
        var sampler = new DisplaySampler(new SwitchableProbe { Answer = _windowed }, () => clock.Now);
        bool[] pausedAt = [false, false, true, true, false];

        foreach (bool paused in pausedAt)
        {
            sampler.Sample(_pid, region: null, paused);
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        sampler.Finish(paused: false);

        sampler.Summary()!.WindowedMs.Should().Be(3000, "five seconds sampled, two of them ending on a paused tick");
    }

    [Fact]
    public void AStaleSwapChainSampleKeepsItsWindowButLosesItsExclusiveBits()
    {
        var clock = new Clock();
        var sampler = new DisplaySampler(new SwitchableProbe { Answer = _covering }, () => clock.Now);
        long sampledAt = clock.Now;
        clock.Advance(DisplaySampler.FreshFor + TimeSpan.FromMilliseconds(1));

        sampler.Sample(_pid, Region(sampledAt, exclusive: true), paused: false);
        clock.Advance(TimeSpan.FromSeconds(1));
        sampler.Finish(paused: false);

        DisplaySummary summary = sampler.Summary()!;
        summary.ExclusiveMs.Should().Be(0, "a minimised game presents only what the hook drops, so an old 'exclusive' says nothing about now");
        summary.CoversMs.Should().Be(1000);
    }

    [Fact]
    public void AnExclusiveChainWhoseWindowCannotBeReadIsStillExclusive()
    {
        var clock = new Clock();
        var sampler = new DisplaySampler(new SwitchableProbe { Answer = null }, () => clock.Now);

        sampler.Sample(_pid, Region(clock.Now, exclusive: true), paused: false);
        clock.Advance(TimeSpan.FromSeconds(1));
        sampler.Sample(_pid, Region(clock.Now, exclusive: false), paused: false);
        clock.Advance(TimeSpan.FromSeconds(1));
        sampler.Finish(paused: false);

        DisplaySummary summary = sampler.Summary()!;
        summary.ExclusiveMs.Should().Be(1000);
        summary.NoWindowMs.Should().Be(1000, "not exclusive and no window is no answer at all");
        summary.ObservedMs.Should().Be(1000, "time without a window is outside every share");
    }

    [Fact]
    public void WithoutAProbeNothingIsSampled()
    {
        var sampler = new DisplaySampler(probe: null);

        sampler.Sample(_pid, region: null, paused: false);
        sampler.Finish(paused: false);

        sampler.Summary().Should().BeNull();
    }

    [Theory]
    [InlineData(0u, null)]
    [InlineData(1u, "discard")]
    [InlineData(2u, "sequential")]
    [InlineData(4u, "flip_sequential")]
    [InlineData(5u, "flip_discard")]
    [InlineData(9u, "effect_8")]
    public void TheSwapEffectIsNamedFromItsValuePlusOne(uint stored, string? expected) =>
        DisplaySampler.SwapEffectName(stored).Should().Be(expected, "0 is 'not read', because DXGI_SWAP_EFFECT_DISCARD is itself 0");

    [Fact]
    public void ARegionThatDescribedNothingIsNoSwapChain()
    {
        DisplaySampler.ViewOf(new FlDisplayState { Flags = FlDisplayFlags.None, Hwnd = 7 }, 0).Should().BeNull();
        DisplaySampler.ViewOf(null, 0).Should().BeNull();
    }

    [Fact]
    public void AnOpenGlRegionHasNoExclusiveAnswer()
    {
        long now = Stopwatch.GetTimestamp();
        SwapChainView view = DisplaySampler.ViewOf(new FlDisplayState { Flags = FlDisplayFlags.Sampled | FlDisplayFlags.OpenGl, Hwnd = 9, SampleQpc = (ulong)now }, now)!.Value;

        view.OpenGl.Should().BeTrue();
        view.ExclusiveKnown.Should().BeFalse();
        DisplayModeClassifier.Classify(view, _covering).Mode.Should().Be(DisplayMode.CoversScreen);
        DisplayModeClassifier.Classify(view, _covering).Source.Should().Be(DisplaySource.OpenGl);
    }
}
