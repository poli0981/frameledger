// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Display;

namespace FrameLedger.Domain.Tests.Display;

/// <summary>
/// The display-mode ladder and its time tally (beta.10, <c>03_METRICS</c> §Display mode): what one sample is, and what a
/// session's samples add up to.
/// </summary>
public sealed class DisplayModeTests
{
    private static readonly ScreenRect _monitor = new(0, 0, 2560, 1440);

    private static WindowView Window(ScreenRect client, bool minimized = false) => new(minimized, client, _monitor, 144);

    private static SwapChainView Chain(bool known = true, bool exclusive = false, bool fresh = true, bool openGl = false) =>
        new(Hwnd: 0x1234, ExclusiveKnown: known, Exclusive: exclusive, OpenGl: openGl, BufferWidth: 2560, BufferHeight: 1440,
            SwapEffect: openGl ? null : "flip_discard", Fresh: fresh);

    [Fact]
    public void AnExclusiveChainIsExclusiveFullscreen() =>
        DisplayModeClassifier.Classify(Chain(exclusive: true), Window(_monitor)).Mode.Should().Be(DisplayMode.ExclusiveFullscreen);

    [Fact]
    public void ACoveringWindowOnANonExclusiveChainIsBorderless()
    {
        DisplayObservation o = DisplayModeClassifier.Classify(Chain(), Window(_monitor));

        o.Mode.Should().Be(DisplayMode.Borderless);
        o.Source.Should().Be(DisplaySource.SwapChain);
        o.Client.Should().Be(_monitor);
        o.BufferWidth.Should().Be(2560u);
        o.MonitorHz.Should().Be(144);
        o.SwapEffect.Should().Be("flip_discard");
    }

    /// <summary>OpenGL, Vulkan and a game that was not hooked cannot be asked: a covering window is "borderless or fullscreen".</summary>
    [Fact]
    public void ACoveringWindowNobodyCouldAskAboutCoversTheScreen()
    {
        DisplayModeClassifier.Classify(null, Window(_monitor)).Should().Match<DisplayObservation>(o =>
            o.Mode == DisplayMode.CoversScreen && o.Source == DisplaySource.Window);
        DisplayModeClassifier.Classify(Chain(known: false, openGl: true), Window(_monitor)).Should().Match<DisplayObservation>(o =>
            o.Mode == DisplayMode.CoversScreen && o.Source == DisplaySource.OpenGl);
    }

    /// <summary>A sample older than 2 s says nothing about now: a minimised game presents only probes the hook drops.</summary>
    [Fact]
    public void AStaleExclusiveSampleIsNotExclusive() =>
        DisplayModeClassifier.Classify(Chain(exclusive: true, fresh: false), Window(_monitor)).Mode.Should().Be(DisplayMode.CoversScreen);

    [Theory]
    [InlineData(100, 100, 1700, 1000)]      // a window in the middle of the screen
    [InlineData(0, 31, 2560, 1400)]         // a maximised window: title bar above, taskbar below
    public void AClientAreaThatDoesNotCoverTheMonitorIsWindowed(int left, int top, int right, int bottom) =>
        DisplayModeClassifier.Classify(Chain(), Window(new ScreenRect(left, top, right, bottom))).Mode.Should().Be(DisplayMode.Windowed);

    [Fact]
    public void OnePixelShortOnAnEdgeStillCovers() =>
        DisplayModeClassifier.Classify(Chain(), Window(new ScreenRect(1, 0, 2559, 1440))).Mode.Should().Be(DisplayMode.Borderless);

    [Fact]
    public void AMinimisedWindowIsMinimisedWhateverTheChainLastSaid() =>
        DisplayModeClassifier.Classify(Chain(exclusive: true), Window(_monitor, minimized: true)).Mode.Should().Be(DisplayMode.Minimized);

    [Fact]
    public void NoWindowIsNoWindowUnlessTheChainSaidExclusive()
    {
        DisplayModeClassifier.Classify(null, null).Should().Match<DisplayObservation>(o => o.Mode == DisplayMode.NoWindow && o.Source == null);
        DisplayModeClassifier.Classify(Chain(), null).Mode.Should().Be(DisplayMode.NoWindow);
        DisplayModeClassifier.Classify(Chain(exclusive: true), null).Mode.Should().Be(DisplayMode.ExclusiveFullscreen);
    }

    /// <summary>Two sampling rates, one unit: a 100 ms tick and a 1 s tick each stand for the time until the next.</summary>
    [Fact]
    public void TheTallyAddsTimeAndTheSharesIgnoreTimeWithNoWindow()
    {
        var tally = new DisplayTally();
        DisplayObservation exclusive = DisplayModeClassifier.Classify(Chain(exclusive: true), Window(_monitor));
        DisplayObservation windowed = DisplayModeClassifier.Classify(Chain(), Window(new ScreenRect(100, 100, 1380, 820)));
        DisplayObservation none = DisplayModeClassifier.Classify(null, null);

        for (int i = 0; i < 30; i++)
        {
            tally.Add(exclusive, 100);    // 3 s hooked at 100 ms
        }

        tally.Add(none, 5_000);          // 5 s with no window: outside every share
        tally.Add(windowed, 1_000);      // 1 s of a windowed hold tick
        tally.Add(exclusive, -50);        // a clock that went backwards adds nothing

        DisplaySummary s = tally.Summary();
        s.ExclusiveMs.Should().Be(3_000);
        s.WindowedMs.Should().Be(1_000);
        s.NoWindowMs.Should().Be(5_000);
        s.ObservedMs.Should().Be(4_000);
        s.SharePercent(DisplayMode.ExclusiveFullscreen).Should().Be(75.0);
        s.SharePercent(DisplayMode.Windowed).Should().Be(25.0);
        s.Changes.Should().Be(2, "exclusive → windowed → exclusive; the sample with no window is not a mode");
        s.Dominant.Should().Be(DisplayMode.ExclusiveFullscreen);
        s.WindowWidth.Should().Be(2560, "the sizes are the longest-lasting mode's");
        s.MonitorHz.Should().Be(144);
        s.Source.Should().Be(DisplaySource.SwapChain);
        s.ExclusivityKnown.Should().BeTrue();
    }

    [Fact]
    public void ASessionWhoseExclusivityWasUnknownForAMomentSaysSo()
    {
        var tally = new DisplayTally();
        tally.Add(DisplayModeClassifier.Classify(Chain(), Window(_monitor)), 900);
        tally.Add(DisplayModeClassifier.Classify(null, Window(_monitor)), 100);

        DisplaySummary s = tally.Summary();
        s.BorderlessMs.Should().Be(900);
        s.CoversMs.Should().Be(100);
        s.ExclusivityKnown.Should().BeFalse();
        s.Source.Should().Be(DisplaySource.SwapChain, "the strongest source any sample had");
    }

    [Fact]
    public void NothingObservedHasNoSharesAndNoSizes()
    {
        var tally = new DisplayTally();
        tally.Any.Should().BeFalse();
        tally.Add(DisplayModeClassifier.Classify(null, null), 1_000);

        DisplaySummary s = tally.Summary();
        s.SharePercent(DisplayMode.Windowed).Should().BeNull();
        s.Dominant.Should().BeNull();
        s.WindowWidth.Should().BeNull();
        s.Source.Should().BeNull();
    }
}
