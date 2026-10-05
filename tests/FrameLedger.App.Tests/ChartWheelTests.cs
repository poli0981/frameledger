// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FluentAssertions;
using FrameLedger.App.Charts;
using ScottPlot.WPF;

namespace FrameLedger.App.Tests;

/// <summary>
/// The wheel over a chart (beta.14): ScottPlot zooms on every wheel and never marks it handled, so on a page that scrolls a
/// wheel over a chart zoomed it AND scrolled the page. A plain wheel is the page's now and Ctrl+wheel the chart's. The Ctrl
/// half reads <see cref="Keyboard.Modifiers"/>, which a test cannot press, so it is held by the pure rule; the plain half is
/// driven end to end.
/// </summary>
public sealed class ChartWheelTests
{
    [Theory]
    [InlineData(ModifierKeys.None, false)]
    [InlineData(ModifierKeys.Shift, false)]
    [InlineData(ModifierKeys.Alt, false)]
    [InlineData(ModifierKeys.Control, true)]
    [InlineData(ModifierKeys.Control | ModifierKeys.Shift, true)]
    public void OnlyCtrlZooms(ModifierKeys modifiers, bool zooms) => ChartWheel.Zooms(modifiers).Should().Be(zooms);

    /// <summary>
    /// A plain wheel over the plot reaches the page and never the plot's drawing surface, where ScottPlot listens: the probe
    /// subscribes to the surface's wheel exactly as ScottPlot does, so it is called whenever ScottPlot would zoom.
    /// </summary>
    [Fact]
    public async Task APlainWheelOverAChartScrollsThePageAndNotTheChart()
    {
        (double Offset, int SurfaceWheels) result = await PagesLoadTests.OnStaAsync(() =>
        {
            var plot = new WpfPlot { Height = 300 };
            ChartTheme.Attach(plot);
            var page = new ScrollViewer
            {
                Height = 400,
                Content = new StackPanel { Children = { new Border { Height = 200 }, plot, new Border { Height = 900 } } },
            };
            Visuals.Layout(page, 800, 400);

            UIElement surface = Visuals.FindAll<FrameworkElement>(plot).First(static e => string.Equals(e.GetType().Name, "SKElement", StringComparison.Ordinal));
            int surfaceWheels = 0;
            surface.MouseWheel += (_, _) => surfaceWheels++;
            var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
            surface.RaiseEvent(wheel);
            wheel.RoutedEvent = UIElement.MouseWheelEvent;
            surface.RaiseEvent(wheel);
            page.UpdateLayout();
            return (page.VerticalOffset, surfaceWheels);
        });

        result.Offset.Should().BeGreaterThan(0, "a plain wheel over a chart scrolls the page it is on");
        result.SurfaceWheels.Should().Be(0, "and never reaches the surface ScottPlot zooms from: zoom is Ctrl+wheel");
    }
}
