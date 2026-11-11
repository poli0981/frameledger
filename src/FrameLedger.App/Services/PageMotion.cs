// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using Wpf.Ui.Animations;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>
/// A page slides in only while Windows' animation effects are on (beta.35): Settings ▸ Accessibility ▸ Visual effects ▸
/// Animation effects, which WPF reads as <see cref="SystemParameters.ClientAreaAnimation"/> and announces when it changes.
/// WPF UI 4.3.0's navigation plays its own <see cref="Transition.FadeInWithSlide"/> whatever that setting says.
/// </summary>
public sealed class PageMotion : IDisposable
{
    private readonly NavigationView _navigation;
    private readonly Func<bool> _animations;

    private PageMotion(NavigationView navigation, Func<bool> animations)
    {
        _navigation = navigation;
        _animations = animations;
        Apply();
        SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;
    }

    /// <summary>The transition for the setting: WPF UI's own while animations are on, none while they are off.</summary>
    public static Transition For(bool animations) => animations ? Transition.FadeInWithSlide : Transition.None;

    /// <summary>
    /// Sets <paramref name="navigation"/>'s transition now and whenever the setting changes, until disposed (the shell does
    /// when its window closes).
    /// </summary>
    public static PageMotion Follow(NavigationView navigation, Func<bool>? animations = null)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        return new PageMotion(navigation, animations ?? (static () => SystemParameters.ClientAreaAnimation));
    }

    public void Dispose() => SystemParameters.StaticPropertyChanged -= OnSystemParameterChanged;

    /// <summary>Reads the setting again — what a change does; a test calls it with its own reader.</summary>
    internal void Apply() => _navigation.Transition = For(_animations());

    [SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "Windows' setting change can arrive off the UI thread; the navigation is its dispatcher's to change, posted and never waited on")]
    private void OnSystemParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(SystemParameters.ClientAreaAnimation), StringComparison.Ordinal))
        {
            _ = _navigation.Dispatcher.BeginInvoke(new Action(Apply));
        }
    }
}
