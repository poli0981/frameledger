// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Automation;
using System.Windows.Automation.Peers;
using ScottPlot.WPF;

namespace FrameLedger.App.Charts;

/// <summary>
/// The App's chart (beta.35, D90): ScottPlot's <see cref="WpfPlot"/> with a voice. ScottPlot's control is a Tab stop with no
/// automation peer, so a screen reader reached every chart and said nothing. This one is an Image a screen reader names —
/// the chart's <c>AutomationProperties.Name</c> — whose help text says where its numbers are written as text and which keys
/// move it (<see cref="ChartKeys"/>). Everything else is ScottPlot's: the subclass keeps its default style and its input.
/// </summary>
public sealed class ChartPlot : WpfPlot
{
    public ChartPlot()
    {
        // The keyboard's focus is drawn as on every other control (WPF UI's focus rectangle), not left to ScottPlot's surface.
        SetResourceReference(FocusVisualStyleProperty, "DefaultControlFocusVisualStyle");
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ChartPlotAutomationPeer(this);

    /// <summary>A chart to a screen reader: an image named by the chart, with where its numbers are and its keys as help.</summary>
    private sealed class ChartPlotAutomationPeer(ChartPlot owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override string GetLocalizedControlTypeCore() => Strings.A11y_Chart_ControlType;

        protected override string GetClassNameCore() => nameof(ChartPlot);

        protected override string GetHelpTextCore()
        {
            string where = AutomationProperties.GetHelpText(Owner);
            return string.IsNullOrWhiteSpace(where) ? Strings.A11y_Chart_Keys : where + " " + Strings.A11y_Chart_Keys;
        }

        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}
