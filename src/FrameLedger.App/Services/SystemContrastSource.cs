// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;

namespace FrameLedger.App.Services;

/// <summary>
/// <see cref="IContrastSource"/> over Windows (beta.35, D91): <see cref="SystemParameters.HighContrast"/>, which WPF updates
/// and announces on Windows' setting change, and the user-preference event Windows raises when a contrast theme's colours
/// are edited while it is on.
/// </summary>
public sealed class SystemContrastSource : IContrastSource, IDisposable
{
    public SystemContrastSource()
    {
        SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public event EventHandler? Changed;

    public bool IsOn => SystemParameters.HighContrast;

    public ContrastPalette Palette => ContrastPalette.FromSystem();

    public void Dispose()
    {
        SystemParameters.StaticPropertyChanged -= OnSystemParameterChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void OnSystemParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(SystemParameters.HighContrast), StringComparison.Ordinal))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (IsOn && e.Category is UserPreferenceCategory.Color or UserPreferenceCategory.Accessibility or UserPreferenceCategory.VisualStyle)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
