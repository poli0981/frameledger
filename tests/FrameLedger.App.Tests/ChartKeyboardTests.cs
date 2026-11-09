// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Input;
using FluentAssertions;
using FrameLedger.App.Charts;
using ScottPlot.WPF;

namespace FrameLedger.App.Tests;

/// <summary>
/// What a chart does with the keyboard (beta.35): <c>08_UI</c> §Accessibility said ScottPlot plots take no focus and no
/// keys, and they take both. A chart is a Tab stop, and the keys ScottPlot reads reach it — the arrows pan, Ctrl with an
/// arrow zooms, A shows all of it. Driven in a real window off the screen, since a key needs the window's input source.
/// </summary>
public sealed class ChartKeyboardTests
{
    [Fact]
    public async Task AFocusedChartPansWithTheArrowsZoomsWithCtrlAndShowsAllWithA()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            var plot = new WpfPlot { Width = 600, Height = 300 };
            ChartTheme.Attach(plot);
            _ = plot.Plot.Add.Signal(new double[] { 0, 1, 0, 1, 0, 1, 0 });
            Window window = ScrollBarHidingTests.OffScreen(new Window { Content = plot });
            try
            {
                window.Show();
                Visuals.Pump();
                plot.Plot.Axes.SetLimits(-5, 5, -1, 1);
                plot.Refresh();
                Visuals.Pump();

                ScottPlot.AxisLimits start = plot.Plot.Axes.GetLimits();
                Press(plot, System.Windows.Input.Key.Left, down: true);
                Press(plot, System.Windows.Input.Key.Left, down: false);
                ScottPlot.AxisLimits panned = plot.Plot.Axes.GetLimits();

                Press(plot, System.Windows.Input.Key.LeftCtrl, down: true);
                Press(plot, System.Windows.Input.Key.Up, down: true);
                Press(plot, System.Windows.Input.Key.Up, down: false);
                Press(plot, System.Windows.Input.Key.LeftCtrl, down: false);
                ScottPlot.AxisLimits zoomed = plot.Plot.Axes.GetLimits();

                Press(plot, System.Windows.Input.Key.A, down: true);
                Press(plot, System.Windows.Input.Key.A, down: false);
                ScottPlot.AxisLimits all = plot.Plot.Axes.GetLimits();

                return new { plot.Focusable, plot.IsTabStop, Start = start, Panned = panned, Zoomed = zoomed, All = all };
            }
            finally
            {
                window.Close();
            }
        });

        facts.Focusable.Should().BeTrue("ScottPlot makes its control focusable; Tab reaches every chart");
        facts.IsTabStop.Should().BeTrue();
        facts.Panned.Left.Should().BeLessThan(facts.Start.Left, "Left moves the view to earlier values");
        (facts.Zoomed.Top - facts.Zoomed.Bottom).Should().BeLessThan(facts.Panned.Top - facts.Panned.Bottom, "Ctrl+Up zooms in vertically");
        facts.All.Left.Should().BeGreaterThan(facts.Panned.Left, "A shows all of the data again");
        facts.All.Right.Should().BeGreaterThanOrEqualTo(6, "the whole signal, seven points from 0 to 6, is in view after A");
    }

    /// <summary>Raises the key the way WPF would: preview, then the bubbling event, from the window's own input source.</summary>
    private static void Press(WpfPlot plot, Key key, bool down)
    {
        PresentationSource source = PresentationSource.FromVisual(plot) ?? throw new InvalidOperationException("the plot has no input source");
        var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = down ? Keyboard.PreviewKeyDownEvent : Keyboard.PreviewKeyUpEvent };
        plot.RaiseEvent(preview);
        var bubble = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = down ? Keyboard.KeyDownEvent : Keyboard.KeyUpEvent };
        plot.RaiseEvent(bubble);
        Visuals.Pump();
    }
}
