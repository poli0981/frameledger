// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>
/// <c>16_WPFUI_SYNTAX</c> §Theme rules: manual Light/Dark through <c>ApplicationThemeManager.Apply</c>; System
/// through <c>SystemThemeWatcher.Watch</c> on the window — which needs an HWND, so the caller passes the window
/// from its <c>Loaded</c> handler and never from a constructor — and <c>UnWatch</c> when switching to manual.
/// </summary>
/// <remarks>
/// <b>A Windows contrast theme wins (beta.35, owner decision D91).</b> While one is on, the App's colours are its colours —
/// the light or the dark Fluent theme as the contrast theme is, under <see cref="ContrastOverlay"/>, with no Mica and no
/// system-theme watcher (WPF UI's would apply its own high-contrast dictionaries, 84 of whose 97 colours are a red
/// placeholder) — whatever Settings ▸ Theme says; the choice applies again when it is turned off. A switch in Windows is
/// followed at once (<see cref="IContrastSource.Changed"/>), and each call is the same for the same state, so the windows that
/// call it from their constructors change nothing.
/// </remarks>
public sealed class WpfThemeApplier : IThemeApplier, IDisposable
{
    private readonly IContrastSource _contrast;
    private AppTheme _theme = AppTheme.System;
    private WeakReference<Window>? _window;

    public WpfThemeApplier(IContrastSource contrast)
    {
        _contrast = contrast ?? throw new ArgumentNullException(nameof(contrast));
        _contrast.Changed += OnContrastChanged;
    }

    public void Apply(AppTheme theme, Window? window)
    {
        _theme = theme;
        if (window is not null)
        {
            _window = new WeakReference<Window>(window);
        }

        Window? watched = window ?? (_window?.TryGetTarget(out Window? last) == true ? last : null);
        bool contrastOn = _contrast.IsOn;
        ContrastPalette? palette = contrastOn ? _contrast.Palette : null;
        ThemePlan plan = ThemePlan.For(theme, contrastOn, palette?.IsDark ?? false, SystemIsDark());
        ResourceDictionary resources = System.Windows.Application.Current.Resources;
        if (plan.Contrast && palette is not null)
        {
            if (watched is not null)
            {
                SystemThemeWatcher.UnWatch(watched);
            }

            // The charts re-read their palette on the theme manager's Changed, which Apply raises: say the contrast first.
            ContrastTheme.Active = palette;
            ApplicationThemeManager.Apply(plan.Theme, plan.Backdrop, updateAccent: false);
            ContrastOverlay.Install(resources, palette);
            Backdrops(plan.Backdrop);
            return;
        }

        bool wasContrast = ContrastOverlay.IsInstalled(resources);
        ContrastOverlay.Remove(resources);
        if (plan.WatchSystem)
        {
            ApplicationThemeManager.Apply(plan.Theme, plan.Backdrop, true);
            if (watched is { IsLoaded: true })
            {
                SystemThemeWatcher.Watch(watched, plan.Backdrop, true);
            }
        }
        else
        {
            if (watched is not null)
            {
                SystemThemeWatcher.UnWatch(watched);
            }

            ApplicationThemeManager.Apply(plan.Theme, plan.Backdrop, true);
        }

        if (wasContrast)
        {
            Backdrops(plan.Backdrop);
        }
    }

    public void Dispose() => _contrast.Changed -= OnContrastChanged;

    private static bool SystemIsDark() => ApplicationThemeManager.GetSystemTheme() is SystemTheme.Dark or SystemTheme.HCBlack or SystemTheme.Glow or SystemTheme.CapturedMotion;

    /// <summary>Every FluentWindow's backdrop now, and again once a window being built has set its own from its XAML.</summary>
    [SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "a WPF application has no JoinableTaskFactory; this posts to the windows' own dispatcher, after a window being built has loaded its XAML, and never waits (ScrollBarHiding does the same)")]
    private static void Backdrops(WindowBackdropType backdrop)
    {
        static void Sweep(WindowBackdropType type)
        {
            foreach (FluentWindow window in System.Windows.Application.Current?.Windows.OfType<FluentWindow>() ?? [])
            {
                window.WindowBackdropType = type;
            }
        }

        Sweep(backdrop);
        _ = System.Windows.Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => Sweep(backdrop));
    }

    [SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "Windows' setting change can arrive off the UI thread; the theme is the dispatcher's to change, posted and never waited on")]
    private void OnContrastChanged(object? sender, EventArgs e)
    {
        Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        _ = dispatcher.BeginInvoke(() => Apply(_theme, _window?.TryGetTarget(out Window? window) == true ? window : null));
    }
}
