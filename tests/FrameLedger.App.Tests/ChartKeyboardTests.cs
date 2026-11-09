// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using FluentAssertions;
using FrameLedger.App.Charts;
using ScottPlot.Interactivity.UserActionResponses;
using ScottPlot.WPF;

namespace FrameLedger.App.Tests;

/// <summary>
/// What a chart does with the keyboard and says to a screen reader (beta.35, D90). <c>08_UI</c> §Accessibility said ScottPlot
/// plots take no focus and no keys; they take both — the arrows pan, Ctrl with an arrow zooms, A shows all of it — and until
/// beta.35 marked no key handled, so the page around a chart acted on the same arrow too. Driven in a real window off the
/// screen, since a key needs the window's input source.
/// </summary>
public sealed class ChartKeyboardTests
{
    [Fact]
    public async Task AFocusedChartPansWithTheArrowsZoomsWithCtrlShowsAllWithAAndKeepsTheKeys()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            var plot = new ChartPlot { Width = 600, Height = 300 };
            ChartTheme.Attach(plot);
            _ = plot.Plot.Add.Signal(new double[] { 0, 1, 0, 1, 0, 1, 0 });
            var below = new Button { Content = "below" };
            var page = new ScrollViewer { Content = new StackPanel { Children = { plot, below, new Border { Height = 2000 } } } };
            Window window = ScrollBarHidingTests.OffScreen(new Window { Content = page });
            try
            {
                window.Show();
                Visuals.Pump();
                plot.Plot.Axes.SetLimits(-5, 5, -1, 1);
                plot.Refresh();
                Visuals.Pump();

                ScottPlot.AxisLimits start = plot.Plot.Axes.GetLimits();
                bool leftHandled = Press(plot, Key.Left, down: true);
                _ = Press(plot, Key.Left, down: false);
                ScottPlot.AxisLimits panned = plot.Plot.Axes.GetLimits();

                _ = Press(plot, Key.LeftCtrl, down: true);
                _ = Press(plot, Key.Up, down: true);
                _ = Press(plot, Key.Up, down: false);
                _ = Press(plot, Key.LeftCtrl, down: false);
                ScottPlot.AxisLimits zoomed = plot.Plot.Axes.GetLimits();

                bool aHandled = Press(plot, Key.A, down: true);
                _ = Press(plot, Key.A, down: false);
                ScottPlot.AxisLimits all = plot.Plot.Axes.GetLimits();
                bool tabHandled = Press(plot, Key.Tab, down: true);

                return new { plot.Focusable, plot.IsTabStop, Start = start, Panned = panned, Zoomed = zoomed, All = all, leftHandled, aHandled, tabHandled, page.VerticalOffset };
            }
            finally
            {
                window.Close();
            }
        });

        facts.Focusable.Should().BeTrue("Tab reaches every chart");
        facts.IsTabStop.Should().BeTrue();
        facts.Panned.Left.Should().BeLessThan(facts.Start.Left, "Left moves the view to earlier values");
        (facts.Zoomed.Top - facts.Zoomed.Bottom).Should().BeLessThan(facts.Panned.Top - facts.Panned.Bottom, "Ctrl+Up zooms in vertically");
        facts.All.Left.Should().BeGreaterThan(facts.Panned.Left, "A shows all of the data again");
        facts.All.Right.Should().BeGreaterThanOrEqualTo(6, "the whole signal, seven points from 0 to 6, is in view after A");
        facts.leftHandled.Should().BeTrue("the arrow is the chart's: the page around it neither scrolls nor moves the focus on (ChartKeys)");
        facts.aHandled.Should().BeTrue();
        facts.tabHandled.Should().BeFalse("Tab still leaves the chart");
        facts.VerticalOffset.Should().Be(0, "the page did not scroll on the chart's keys");
    }

    /// <summary>beta.35 (D90): a double-click shows all of it, as the guide says; ScottPlot's own toggled its benchmark overlay.</summary>
    [Fact]
    public async Task ADoubleClickShowsAllOfTheChartAndTheBenchmarkIsGone()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            var plot = new WpfPlot();
            ChartTheme.Attach(plot);
            ChartTheme.ResetOnDoubleClick(plot);   // a second call adds nothing
            return new
            {
                Benchmarks = plot.UserInputProcessor.UserActionResponses.OfType<DoubleClickBenchmark>().Count(),
                Resets = plot.UserInputProcessor.UserActionResponses.OfType<DoubleClickResponse>().Count(),
            };
        });

        facts.Benchmarks.Should().Be(0, "a double-click is no developer's render-time overlay");
        facts.Resets.Should().Be(1);
    }

    [Fact]
    public async Task AChartIsANamedImageThatSaysWhereItsNumbersAreAndItsKeys()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            var plot = new ChartPlot();
            AutomationProperties.SetName(plot, "Frame time chart");
            AutomationProperties.SetHelpText(plot, "The figures are above.");
            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(plot)!;
            AutomationPeer bare = UIElementAutomationPeer.CreatePeerForElement(new ChartPlot())!;
            return new
            {
                Type = peer.GetAutomationControlType(),
                Localized = peer.GetLocalizedControlType(),
                Name = peer.GetName(),
                Help = peer.GetHelpText(),
                BareHelp = bare.GetHelpText(),
                Children = peer.GetChildren(),
                Class = peer.GetClassName(),
                Keys = Strings.A11y_Chart_Keys,
                Kind = Strings.A11y_Chart_ControlType,
            };
        });

        facts.Type.Should().Be(AutomationControlType.Image);
        facts.Localized.Should().Be(facts.Kind, "a screen reader says 'chart', in the UI's language");
        facts.Name.Should().Be("Frame time chart");
        facts.Help.Should().StartWith("The figures are above.").And.EndWith(facts.Keys, "where its numbers are, then its keys");
        facts.BareHelp.Should().Be(facts.Keys);
        facts.Children.Should().BeNull("ScottPlot's surface is pixels; nothing inside is read");
        facts.Class.Should().Be(nameof(ChartPlot));
    }

    /// <summary>Raises the key the way WPF would: preview, then the bubbling event, from the window's own input source; whether it ended handled.</summary>
    private static bool Press(WpfPlot plot, Key key, bool down)
    {
        PresentationSource source = PresentationSource.FromVisual(plot) ?? throw new InvalidOperationException("the plot has no input source");
        var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = down ? Keyboard.PreviewKeyDownEvent : Keyboard.PreviewKeyUpEvent };
        plot.RaiseEvent(preview);
        var bubble = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = down ? Keyboard.KeyDownEvent : Keyboard.KeyUpEvent };
        plot.RaiseEvent(bubble);
        Visuals.Pump();
        return bubble.Handled;
    }
}
