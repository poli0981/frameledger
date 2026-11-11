// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>
/// What a theme choice comes to (beta.35, D91): the Fluent theme, the windows' backdrop, whether WPF UI watches Windows'
/// own theme, and whether a contrast theme's colours go over it. A contrast theme wins over the App's choice while it is on —
/// the theme under it is the light or the dark one as the contrast theme is, with no Mica — and the choice applies again
/// when it is off.
/// </summary>
public readonly record struct ThemePlan(ApplicationTheme Theme, WindowBackdropType Backdrop, bool WatchSystem, bool Contrast)
{
    /// <summary>The plan for a choice in a state of Windows.</summary>
    /// <param name="choice">Settings ▸ Theme.</param>
    /// <param name="contrastOn">A Windows contrast theme is on.</param>
    /// <param name="contrastDark">That contrast theme is a dark one (its window colour).</param>
    /// <param name="systemDark">Windows' own app mode is dark (for <see cref="AppTheme.System"/>).</param>
    public static ThemePlan For(AppTheme choice, bool contrastOn, bool contrastDark, bool systemDark)
    {
        if (contrastOn)
        {
            return new(contrastDark ? ApplicationTheme.Dark : ApplicationTheme.Light, WindowBackdropType.None, WatchSystem: false, Contrast: true);
        }

        return choice switch
        {
            AppTheme.System => new(systemDark ? ApplicationTheme.Dark : ApplicationTheme.Light, WindowBackdropType.Mica, WatchSystem: true, Contrast: false),
            AppTheme.Light => new(ApplicationTheme.Light, WindowBackdropType.Mica, WatchSystem: false, Contrast: false),
            AppTheme.Dark => new(ApplicationTheme.Dark, WindowBackdropType.Mica, WatchSystem: false, Contrast: false),
            _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "not a theme"),
        };
    }
}
