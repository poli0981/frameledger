// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ScottPlot.WPF;

namespace FrameLedger.App.Charts;

/// <summary>
/// The mouse wheel over a chart (beta.14). ScottPlot 5's <see cref="WpfPlot"/> zooms on every wheel and never marks it
/// handled, so with the pages scrolling again (<c>16_WPFUI_SYNTAX</c> §Gotchas: they did not until beta.14) a wheel over a
/// chart would have zoomed it AND scrolled the page under the pointer. A plain wheel now belongs to the page — ScottPlot
/// never sees it — and Ctrl+wheel to the chart, which zooms and keeps the page where it is. Every plot reaches this through
/// <see cref="ChartTheme.Attach"/>.
/// </summary>
internal static class ChartWheel
{
    /// <summary>Whether a wheel with these modifier keys zooms the chart rather than scrolling the page: Ctrl held.</summary>
    public static bool Zooms(ModifierKeys modifiers) => (modifiers & ModifierKeys.Control) == ModifierKeys.Control;

    public static void Attach(WpfPlot plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        plot.PreviewMouseWheel += OnPreviewMouseWheel;
        plot.MouseWheel += OnMouseWheel;
    }

    /// <summary>
    /// The tunnelling half, before ScottPlot's own handler on its inner element: a plain wheel is taken from the plot and
    /// raised again on the plot's parent, from where it bubbles to the page's scroller as any other wheel would.
    /// </summary>
    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || Zooms(Keyboard.Modifiers) || sender is not DependencyObject plot)
        {
            return;
        }

        e.Handled = true;
        if (VisualTreeHelper.GetParent(plot) is UIElement parent)
        {
            parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = sender });
        }
    }

    /// <summary>The bubbling half, after ScottPlot has zoomed: Ctrl+wheel stops here, so the page does not scroll as well.</summary>
    private static void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Zooms(Keyboard.Modifiers))
        {
            e.Handled = true;
        }
    }
}
