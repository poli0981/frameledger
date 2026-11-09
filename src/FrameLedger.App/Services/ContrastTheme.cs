// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.ComponentModel;

namespace FrameLedger.App.Services;

/// <summary>
/// The contrast theme the App's own colours follow now (beta.35, D91): set while <see cref="ContrastOverlay"/> is installed,
/// null otherwise. What WPF UI's theme resources cannot carry reads it — the charts' palette, the frame-generation bar, the
/// change badges, the artwork's tint. The UI thread's: set and read there.
/// </summary>
public static class ContrastTheme
{
    private static ContrastPalette? _active;

    /// <summary>WPF's change notice for a static property: what a binding to <see cref="IsOn"/> listens to, weakly.</summary>
    public static event EventHandler<PropertyChangedEventArgs>? StaticPropertyChanged;

    /// <summary>The palette, or null while no contrast theme is followed.</summary>
    public static ContrastPalette? Active
    {
        get => _active;
        internal set
        {
            if (Equals(_active, value))
            {
                return;
            }

            bool was = IsOn;
            _active = value;
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(Active)));
            if (was != IsOn)
            {
                StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(IsOn)));
            }
        }
    }

    /// <summary>
    /// A contrast theme is followed now — what XAML binds to, <c>{Binding Path=(services:ContrastTheme.IsOn)}</c>, to change
    /// while it is shown: the Remove button's look (<c>DangerButton</c>), the line under Settings ▸ Theme.
    /// </summary>
    public static bool IsOn => _active is not null;
}
