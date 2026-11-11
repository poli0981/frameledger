// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.ComponentModel;

namespace FrameLedger.App.Services;

/// <summary>
/// Whether the Advanced page is offered (owner decisions D101 and D105): its navigation item is there, and Ctrl+6
/// opens it, only while this is on. Set at start from <c>telemetry.advanced</c> and again whenever Settings moves the
/// switch; in a viewer it is also on when the copy holds a session recorded with the option on, since that is what a
/// copy is opened to look at.
/// </summary>
/// <remarks>
/// A static that XAML binds to, as <see cref="ContrastTheme"/> is: the shell's navigation item lives in the window's
/// XAML, which a language change rebuilds, and a setting read once at start would not follow the switch.
/// </remarks>
public static class AdvancedOption
{
    private static bool _isOn;

    /// <summary>WPF's change notice for a static property: what a binding to <see cref="IsOn"/> listens to, weakly.</summary>
    public static event EventHandler<PropertyChangedEventArgs>? StaticPropertyChanged;

    public static bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn == value)
            {
                return;
            }

            _isOn = value;
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(IsOn)));
        }
    }
}
