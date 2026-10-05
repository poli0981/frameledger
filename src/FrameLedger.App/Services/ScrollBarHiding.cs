// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FrameLedger.App.Services;

/// <summary>
/// <c>ui.hide_scrollbars</c> (beta.14, owner request 2026-10-05): the App draws no scroll bars, and every scroller still
/// scrolls — with the wheel, the keyboard and touch. A bar's visibility is bound in its scroller's template, so no style can
/// reach it; this sets the scroller's own <see cref="ScrollBarVisibility"/> instead. One class handler on
/// <see cref="ScrollViewer"/>'s <c>Loaded</c>, registered at start beside <see cref="DialogKeyboard"/>, sees every scroller the
/// App shows, those inside a control's template too (a DataGrid's, a TextBox's, a rendered document's, a dialog's). While
/// on, an axis whose bar is <see cref="ScrollBarVisibility.Auto"/> or <see cref="ScrollBarVisibility.Visible"/> becomes
/// <see cref="ScrollBarVisibility.Hidden"/> — WPF's "no bar, still scrolls" — and its own value is remembered on it;
/// <see cref="ScrollBarVisibility.Disabled"/> is never touched, because a disabled axis lays its content out at the viewport's
/// size and wraps text. Off puts every remembered value back, on screen at once and on the next load for the rest.
/// </summary>
public static class ScrollBarHiding
{
    private static readonly DependencyProperty _ownVerticalProperty =
        DependencyProperty.RegisterAttached("OwnVertical", typeof(ScrollBarVisibility?), typeof(ScrollBarHiding), new PropertyMetadata(null));

    private static readonly DependencyProperty _ownHorizontalProperty =
        DependencyProperty.RegisterAttached("OwnHorizontal", typeof(ScrollBarVisibility?), typeof(ScrollBarHiding), new PropertyMetadata(null));

    private static volatile bool _hidden;
    private static int _registered;

    /// <summary>Whether the bars are hidden right now.</summary>
    public static bool Hidden => _hidden;

    /// <summary>Registers the class handler, once per process.</summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 0)
        {
            EventManager.RegisterClassHandler(typeof(ScrollViewer), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded), handledEventsToo: true);
        }
    }

    /// <summary>
    /// Sets the preference and applies it to every scroller of every open window — at once on the App's own thread, where
    /// Settings changes it; from any other thread the windows are visited when that thread's dispatcher next runs.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "a WPF application has no JoinableTaskFactory; the windows belong to the dispatcher's thread, and posting to it never blocks (UiThread does the same)")]
    public static void Apply(bool hidden)
    {
        _hidden = hidden;
        if (System.Windows.Application.Current is not { } app)
        {
            return;
        }

        if (app.Dispatcher.CheckAccess())
        {
            ApplyToWindows(app);
        }
        else
        {
            _ = app.Dispatcher.InvokeAsync(() => ApplyToWindows(app));
        }
    }

    private static void ApplyToWindows(System.Windows.Application app)
    {
        foreach (Window window in app.Windows)
        {
            ApplyUnder(window);
        }
    }

    /// <summary>Applies the current preference to every scroller under <paramref name="root"/>: an open window, or a test's tree.</summary>
    public static void ApplyUnder(DependencyObject root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (root is ScrollViewer viewer)
        {
            Set(viewer);
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            ApplyUnder(VisualTreeHelper.GetChild(root, i));
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer viewer)
        {
            Set(viewer);
        }
    }

    private static void Set(ScrollViewer viewer)
    {
        Axis(viewer, ScrollViewer.VerticalScrollBarVisibilityProperty, _ownVerticalProperty);
        Axis(viewer, ScrollViewer.HorizontalScrollBarVisibilityProperty, _ownHorizontalProperty);
    }

    private static void Axis(ScrollViewer viewer, DependencyProperty axis, DependencyProperty own)
    {
        var remembered = (ScrollBarVisibility?)viewer.GetValue(own);
        if (_hidden)
        {
            if (remembered is null && viewer.GetValue(axis) is ScrollBarVisibility current and (ScrollBarVisibility.Auto or ScrollBarVisibility.Visible))
            {
                viewer.SetValue(own, current);
                viewer.SetCurrentValue(axis, ScrollBarVisibility.Hidden);
            }
        }
        else if (remembered is { } value)
        {
            viewer.ClearValue(own);
            viewer.SetCurrentValue(axis, value);
        }
    }
}
