// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FrameLedger.App.Services;

/// <summary>
/// Ctrl+1 … Ctrl+5 put the keyboard in the page they open (beta.35): once it is shown, its first control takes the focus
/// and a screen reader hears its title — its first-level heading. Before, the focus stayed on the control that had it,
/// often one of the page that had just gone, and Tab began again at the top of the window. A page chosen in the pane with
/// the mouse leaves the focus where WPF UI puts it.
/// </summary>
public sealed class PageFocus
{
    private readonly IAnnouncer _announcer;
    private WeakReference<FrameworkElement>? _page;

    public PageFocus(IAnnouncer? announcer = null) => _announcer = announcer ?? NoAnnouncer.Instance;

    /// <summary>The shell's word of the page shown now, after every navigation, by a shortcut or not.</summary>
    public void Shown(FrameworkElement page)
    {
        ArgumentNullException.ThrowIfNull(page);
        _page = new WeakReference<FrameworkElement>(page);
    }

    /// <summary>
    /// After a shortcut has navigated — or found its page already shown, when WPF UI navigates nowhere — the page shown then
    /// takes the focus once the dispatcher has laid it out.
    /// </summary>
    [SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "a WPF application has no JoinableTaskFactory; this posts to the UI thread's own dispatcher, after the new page's layout, and never waits (ScrollBarHiding does the same)")]
    public void Request()
    {
        Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        _ = dispatcher.BeginInvoke(DispatcherPriority.Loaded, Take);
    }

    /// <summary>Moves the focus to the page's first control and says its title.</summary>
    internal void Enter(FrameworkElement page)
    {
        ArgumentNullException.ThrowIfNull(page);
        _ = page.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        if (Title(page) is { Length: > 0 } title)
        {
            _announcer.Announce(title, AnnouncementKind.Status, "Page");
        }
    }

    /// <summary>The page's title: the text of its first first-level heading (beta.35, D90 — every page has exactly one).</summary>
    internal static string? Title(DependencyObject root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (root is TextBlock text && AutomationProperties.GetHeadingLevel(text) == AutomationHeadingLevel.Level1)
        {
            return text.Text;
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            if (Title(VisualTreeHelper.GetChild(root, i)) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private void Take()
    {
        if (_page?.TryGetTarget(out FrameworkElement? page) != true || page is null)
        {
            return;
        }

        if (page.IsLoaded)
        {
            Enter(page);
            return;
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            page.Loaded -= OnLoaded;
            Enter(page);
        }

        page.Loaded += OnLoaded;
    }
}
