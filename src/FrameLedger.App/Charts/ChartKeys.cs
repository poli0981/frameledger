// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Input;
using ScottPlot.WPF;

namespace FrameLedger.App.Charts;

/// <summary>
/// The keys of a chart that has the focus (beta.35, D90). ScottPlot reads them — the arrows move the view, Ctrl with an arrow
/// zooms, A shows all of it — and marks none handled, so the page around a chart acted on the same arrow too: it scrolled,
/// or moved the focus on, and a pan lasted one keypress (<c>ChartKeyboardTests</c>). After ScottPlot has acted, these keys
/// are the chart's alone; Tab and every other key go on as before.
/// </summary>
public static class ChartKeys
{
    /// <summary>Whether a key that reached a focused chart is the chart's: an arrow (with or without Ctrl, Shift or Alt) or A.</summary>
    public static bool IsTheCharts(Key key) => key is Key.Left or Key.Right or Key.Up or Key.Down or Key.A;

    /// <summary>
    /// Keeps the chart's keys from the page. An instance handler runs after the class's own — ScottPlot's
    /// <c>OnKeyDown</c> — so the plot has already moved when the key is marked.
    /// </summary>
    public static void Attach(WpfPlot control)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.KeyDown += static (_, e) =>
        {
            if (IsTheCharts(e.Key))
            {
                e.Handled = true;
            }
        };
    }
}
