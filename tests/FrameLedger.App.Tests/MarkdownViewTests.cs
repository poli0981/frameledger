// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FluentAssertions;
using FrameLedger.App.Controls;
using Xunit;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.21 (owner request 2026-10-13: "scrolling the documents in the app is slow"): the wheel moves a document by Windows'
/// lines at a browser's line instead of WPF's fixed 48 DIP, in proportion to the delta, and leaves its ends to the host; a
/// page is rendered once, and a page shown before is not rendered again.
/// </summary>
public sealed class MarkdownViewTests
{
    private static readonly string _longText = string.Join("\n\n", Enumerable.Range(1, 400).Select(static i => "Paragraph " + i + ": a few words that wrap."));

    [Fact]
    public void TheWheelStepFollowsTheDeltaWindowsLinesAndTheZoom()
    {
        MarkdownView.WheelStep(-120, 3, 600, 100).Should().BeApproximately(100, 1e-9, "three lines at a browser's line, downwards");
        MarkdownView.WheelStep(120, 3, 600, 100).Should().BeApproximately(-100, 1e-9);
        MarkdownView.WheelStep(-15, 3, 600, 100).Should().BeApproximately(12.5, 1e-9, "a touchpad's small delta moves in proportion");
        MarkdownView.WheelStep(-120, 3, 600, 200).Should().BeApproximately(200, 1e-9, "zoomed in, a line is taller");
        MarkdownView.WheelStep(-120, -1, 600, 100).Should().BeApproximately(600, 1e-9, "Windows' \"one screen at a time\"");
        MarkdownView.WheelStep(-120, 0, 600, 100).Should().Be(0);
    }

    [Fact]
    public async Task TheWheelMovesTheDocumentByItsStepAndLeavesItsEndsToTheHost()
    {
        (double notch, double touch, bool atEnd, bool withCtrl) = await PagesLoadTests.OnStaAsync(static () =>
        {
            var view = new MarkdownView { EaseWheel = false, WheelScrollLines = static () => 3, CurrentModifiers = static () => ModifierKeys.None };
            view.Show(_longText, isPlainText: false, path: "LIMITATIONS.md");
            Layout(view);
            ScrollViewer scroller = view.Scroller ?? throw new InvalidOperationException("the template has no PART_ContentHost");
            scroller.ScrollableHeight.Should().BeGreaterThan(1000, "the document is longer than its view");

            Wheel(view, -120).Should().BeTrue();
            Layout(view);
            double afterNotch = scroller.VerticalOffset;

            Wheel(view, -15).Should().BeTrue();
            Layout(view);
            double afterTouch = scroller.VerticalOffset;

            scroller.ScrollToVerticalOffset(scroller.ScrollableHeight);
            Layout(view);
            bool handledAtEnd = Wheel(view, -120);

            scroller.ScrollToVerticalOffset(0);
            Layout(view);
            view.CurrentModifiers = static () => ModifierKeys.Control;
            bool handledWithCtrl = Wheel(view, -120);
            return (afterNotch, afterTouch, handledAtEnd, handledWithCtrl);
        });

        notch.Should().BeApproximately(100, 0.5, "one notch is three lines of 100/3 DIP, not WPF's fixed 48");
        touch.Should().BeApproximately(112.5, 0.5, "a delta of 15 is an eighth of a notch");
        atEnd.Should().BeFalse("at the end the wheel is the host's — the update notes' dialog scrolls instead");
        withCtrl.Should().BeFalse("Ctrl+wheel is the zoom's, never the scroll's");
    }

    [Fact]
    public async Task EasingAddsEveryNotchToTheTargetAndANewPageStopsIt()
    {
        (double first, double second, double afterShow) = await PagesLoadTests.OnStaAsync(static () =>
        {
            var view = new MarkdownView { EaseWheel = true, WheelScrollLines = static () => 3, CurrentModifiers = static () => ModifierKeys.None };
            view.Show(_longText, isPlainText: false, path: "LIMITATIONS.md");
            Layout(view);
            Wheel(view, -120).Should().BeTrue();
            double target1 = view.ScrollTarget;
            Wheel(view, -120).Should().BeTrue();
            double target2 = view.ScrollTarget;
            view.Show("# Another page", isPlainText: false, path: "guide/README.md");
            return (target1, target2, view.ScrollTarget);
        });

        first.Should().BeApproximately(100, 0.5);
        second.Should().BeApproximately(200, 0.5, "a notch turned while easing adds to where the view is going, not to where it is");
        double.IsNaN(afterShow).Should().BeTrue("a new page ends the easing");
    }

    [Fact]
    public async Task APageIsRenderedOnceAndAPageShownBeforeIsNotRenderedAgain()
    {
        (int one, int two, int back, int evicted) = await PagesLoadTests.OnStaAsync(static () =>
        {
            var view = new MarkdownView();
            int start = view.Renders;    // the constructor's empty document
            view.Show("# One\n\ntext", isPlainText: false, path: "guide/a.md");
            int afterOne = view.Renders - start;
            view.Show("# Two\n\ntext", isPlainText: false, path: "guide/b.md");
            int afterTwo = view.Renders - start;
            view.Show("# One\n\ntext", isPlainText: false, path: "guide/a.md");
            int afterBack = view.Renders - start;

            for (int i = 0; i < MarkdownView.CacheCapacity; i++)
            {
                view.Show("# Page " + i, isPlainText: false, path: "guide/p" + i + ".md");
            }

            int beforeEvicted = view.Renders;
            view.Show("# Two\n\ntext", isPlainText: false, path: "guide/b.md");
            return (afterOne, afterTwo, afterBack, view.Renders - beforeEvicted);
        });

        one.Should().Be(1, "Show sets the three properties and renders once");
        two.Should().Be(2);
        back.Should().Be(2, "a page this view has rendered is kept");
        evicted.Should().Be(1, "past the cache's capacity the oldest page is rendered again");
    }

    [Fact]
    public async Task PropertiesSetOneByOneAreRenderedOnceAheadOfLayout()
    {
        (int beforePump, int afterPump, bool hasAnchor) = await PagesLoadTests.OnStaAsync(static () =>
        {
            var view = new MarkdownView();
            int start = view.Renders;
            view.DocumentPath = "guide/a.md";
            view.IsPlainText = false;
            view.MarkdownText = "# Heading\n\ntext";
            int before = view.Renders - start;
            Visuals.Pump();
            int after = view.Renders - start;
            return (before, after, view.Anchors.Count > 0);
        });

        beforePump.Should().Be(0, "a binding's change asks for a render; it does not render the half-set page");
        afterPump.Should().Be(1, "three changes, one render");
        hasAnchor.Should().BeTrue();
    }

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(700, 600));
        element.Arrange(new Rect(0, 0, 700, 600));
        element.UpdateLayout();
    }

    private static bool Wheel(UIElement over, int delta)
    {
        var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
        over.RaiseEvent(wheel);
        return wheel.Handled;
    }
}
