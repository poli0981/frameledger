// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Automation.Peers;

namespace FrameLedger.App.Controls;

/// <summary>
/// The voice of a control that only shows something (beta.35, D88) — the FPS readout, a ray-tracing chip, a change badge.
/// A plain <c>Control</c> has no automation peer, and WPF hides the text in its template from the control view, so a
/// screen reader was given nothing for these. This peer is a Text element named by what the control says in words, with
/// no children, so the template's pieces are not read a second time.
/// </summary>
/// <param name="owner">The control.</param>
/// <param name="name">Its words, read when the screen reader asks; the control's own <c>AutomationProperties.Name</c> wins.</param>
/// <param name="help">More words, where the eye has a tooltip; optional.</param>
public sealed class SpokenTextAutomationPeer(FrameworkElement owner, Func<string?> name, Func<string?>? help = null) : FrameworkElementAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

    protected override string GetClassNameCore() => Owner.GetType().Name;

    protected override string GetNameCore()
    {
        string own = base.GetNameCore();
        return own.Length > 0 ? own : name() ?? string.Empty;
    }

    protected override string GetHelpTextCore()
    {
        string own = base.GetHelpTextCore();
        return own.Length > 0 ? own : help?.Invoke() ?? string.Empty;
    }

    protected override List<AutomationPeer>? GetChildrenCore() => null;
}
