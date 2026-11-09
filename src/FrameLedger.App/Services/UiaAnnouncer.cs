// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Threading;

namespace FrameLedger.App.Services;

/// <summary>
/// <see cref="IAnnouncer"/> through UI Automation's notification event (beta.35, D89) — the one place the App raises it.
/// Narrator, NVDA and JAWS read a notification raised on a window's peer without moving the focus. WPF's live regions are not
/// used: WPF raises <c>LiveRegionChanged</c> only when the app does, which is what this is, said once rather than per line.
/// </summary>
/// <remarks>
/// <b>Only the App's active window speaks.</b> While a game has the screen, nothing interrupts it: the notice stays in the
/// window, and the tray's Windows notifications — which a screen reader reads itself — cover a hidden window. Nothing is
/// raised when no screen reader listens.
/// </remarks>
public sealed class UiaAnnouncer : IAnnouncer
{
    private readonly Func<Window?> _target;
    private readonly Func<bool> _listening;
    private readonly Action<AutomationPeer, AutomationNotificationKind, AutomationNotificationProcessing, string, string> _raise;

    /// <summary>The App's: its active window, Windows' own listener check, the peer's own event.</summary>
    public UiaAnnouncer()
        : this(ActiveWindow, Listening, Raise)
    {
    }

    /// <summary>For the tests: where it speaks, whether anyone listens, and how it raises the event.</summary>
    internal UiaAnnouncer(Func<Window?> target, Func<bool> listening, Action<AutomationPeer, AutomationNotificationKind, AutomationNotificationProcessing, string, string> raise)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _listening = listening ?? throw new ArgumentNullException(nameof(listening));
        _raise = raise ?? throw new ArgumentNullException(nameof(raise));
    }

    /// <summary>What the processing of each kind is: every alert read, a status replaced by a newer one of its activity.</summary>
    public static AutomationNotificationProcessing ProcessingOf(AnnouncementKind kind) =>
        kind == AnnouncementKind.Alert ? AutomationNotificationProcessing.ImportantAll : AutomationNotificationProcessing.MostRecent;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "a WPF application has no JoinableTaskFactory; an event from the pipe posts the announcement to the windows' dispatcher and never waits (UiThread does the same)")]
    public void Announce(string text, AnnouncementKind kind, string activity)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(() => Announce(text, kind, activity));
            return;
        }

        if (!_listening() || _target() is not { } window)
        {
            return;
        }

        if ((UIElementAutomationPeer.FromElement(window) ?? UIElementAutomationPeer.CreatePeerForElement(window)) is { } peer)
        {
            _raise(peer, AutomationNotificationKind.Other, ProcessingOf(kind), text, "FrameLedger." + activity);
        }
    }

    private static Window? ActiveWindow() =>
        System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(static w => w is { IsActive: true, IsVisible: true });

    private static bool Listening() => AutomationPeer.ListenerExists(AutomationEvents.Notification);

    private static void Raise(AutomationPeer peer, AutomationNotificationKind kind, AutomationNotificationProcessing processing, string text, string activity) =>
        peer.RaiseNotificationEvent(kind, processing, text, activity);
}
