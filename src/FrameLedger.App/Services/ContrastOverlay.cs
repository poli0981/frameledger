// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Media;

namespace FrameLedger.App.Services;

/// <summary>
/// A contrast theme's colours over the App's Fluent theme (beta.35, D91): every WPF UI theme resource given the user's colour
/// for its role (<see cref="ContrastRoles"/>), as one merged dictionary on top of the theme — the light or the dark one, as
/// the contrast theme is light or dark — and the accent's resources, which WPF UI's accent manager writes straight into the
/// application's resources and which outrank any merged dictionary, written there too. Removing it leaves the Fluent theme;
/// <c>ApplicationThemeManager.Apply(…, updateAccent: true)</c> then writes the accent back.
/// </summary>
public static class ContrastOverlay
{
    /// <summary>
    /// The accent's resources: the 21 WPF UI 4.3.0's <c>ApplicationAccentColorManager</c> sets in the application's own
    /// resources (measured 2026-11-09), each with its role.
    /// </summary>
    public static IReadOnlyDictionary<string, (ContrastRole Role, bool Brush)> AccentKeys { get; } = new Dictionary<string, (ContrastRole, bool)>(StringComparer.Ordinal)
    {
        ["SystemAccentColor"] = (ContrastRole.Highlight, false),
        ["SystemAccentColorPrimary"] = (ContrastRole.Highlight, false),
        ["SystemAccentColorSecondary"] = (ContrastRole.Highlight, false),
        ["SystemAccentColorTertiary"] = (ContrastRole.Highlight, false),
        ["SystemAccentBrush"] = (ContrastRole.Highlight, true),
        ["SystemFillColorAttentionBrush"] = (ContrastRole.Highlight, true),
        ["AccentFillColorDefault"] = (ContrastRole.Highlight, false),
        ["AccentFillColorSecondary"] = (ContrastRole.Highlight, false),
        ["AccentFillColorTertiary"] = (ContrastRole.Highlight, false),
        ["AccentFillColorDefaultBrush"] = (ContrastRole.Highlight, true),
        ["AccentFillColorSecondaryBrush"] = (ContrastRole.Highlight, true),
        ["AccentFillColorTertiaryBrush"] = (ContrastRole.Highlight, true),
        ["AccentFillColorSelectedTextBackgroundBrush"] = (ContrastRole.Highlight, true),
        ["AccentTextFillColorPrimaryBrush"] = (ContrastRole.HotTrack, true),
        ["AccentTextFillColorSecondaryBrush"] = (ContrastRole.HotTrack, true),
        ["AccentTextFillColorTertiaryBrush"] = (ContrastRole.HotTrack, true),
        ["AccentTextFillColorDisabled"] = (ContrastRole.GrayText, false),
        ["TextOnAccentFillColorPrimary"] = (ContrastRole.HighlightText, false),
        ["TextOnAccentFillColorSecondary"] = (ContrastRole.HighlightText, false),
        ["TextOnAccentFillColorSelectedText"] = (ContrastRole.HighlightText, false),
        ["TextOnAccentFillColorDisabled"] = (ContrastRole.GrayText, false),
    };

    /// <summary>
    /// The colours of WPF UI 4.3.0's Fluent themes that none of its contrast dictionaries has (measured 2026-11-09), each with a
    /// role: the navigation pane's separator, a gradient there, a line in the window text; the snowflake effect's two, the window.
    /// </summary>
    public static IReadOnlyDictionary<string, (ContrastRole Role, bool Brush)> FluentOnlyKeys { get; } = new Dictionary<string, (ContrastRole, bool)>(StringComparer.Ordinal)
    {
        ["LeftNavigationViewSeparatorBrush"] = (ContrastRole.WindowText, true),
        ["SnowflakeGradientTop"] = (ContrastRole.Window, false),
        ["SnowflakeGradientBottom"] = (ContrastRole.Window, false),
    };

    /// <summary>
    /// The text on the accent, which the overlay makes the highlight: the highlight's text. WPF UI's contrast dictionaries make
    /// it the window text over an accent they leave as Windows' (measured 2026-11-09: white on Dusk's highlight, 1.62:1, in
    /// every chip the App draws on the accent), so these are the App's roles, not inferred ones.
    /// </summary>
    public static IReadOnlyDictionary<string, (ContrastRole Role, bool Brush)> OnAccentKeys { get; } = new Dictionary<string, (ContrastRole, bool)>(StringComparer.Ordinal)
    {
        ["TextOnAccentFillColorPrimary"] = (ContrastRole.HighlightText, false),
        ["TextOnAccentFillColorPrimaryBrush"] = (ContrastRole.HighlightText, true),
        ["TextOnAccentFillColorSecondary"] = (ContrastRole.HighlightText, false),
        ["TextOnAccentFillColorSecondaryBrush"] = (ContrastRole.HighlightText, true),
        ["TextOnAccentFillColorSelectedText"] = (ContrastRole.HighlightText, false),
        ["TextOnAccentFillColorSelectedTextBrush"] = (ContrastRole.HighlightText, true),
    };

    /// <summary>The overlay: every role-mapped key in the palette's colour for its role, and the transparent keys transparent.</summary>
    public static ResourceDictionary Build(ContrastPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var overlay = new Overlay();
        foreach ((string key, (ContrastRole role, bool brush)) in ContrastRoles.Map)
        {
            overlay[key] = Value(palette.Of(role), brush);
        }

        foreach ((string key, (ContrastRole role, bool brush)) in OnAccentKeys)
        {
            overlay[key] = Value(palette.Of(role), brush);
        }

        foreach ((string key, bool brush) in ContrastRoles.Clear)
        {
            overlay[key] = Value(Colors.Transparent, brush);
        }

        foreach ((string key, (ContrastRole role, bool brush)) in FluentOnlyKeys.Where(k => !overlay.Contains(k.Key)))
        {
            overlay[key] = Value(palette.Of(role), brush);
        }

        return overlay;
    }

    /// <summary>Puts the palette over <paramref name="resources"/> (the application's), replacing an earlier overlay.</summary>
    public static void Install(ResourceDictionary resources, ContrastPalette palette)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(palette);
        TakeOff(resources);
        ContrastTheme.Active = palette;
        resources.MergedDictionaries.Add(Build(palette));
        foreach ((string key, (ContrastRole role, bool brush)) in AccentKeys)
        {
            resources[key] = Value(palette.Of(role), brush);
        }
    }

    /// <summary>Takes the overlay away; the accent's own resources are the theme manager's to write back.</summary>
    public static void Remove(ResourceDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        TakeOff(resources);
        ContrastTheme.Active = null;
    }

    /// <summary>Whether an overlay is on <paramref name="resources"/>.</summary>
    public static bool IsInstalled(ResourceDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return resources.MergedDictionaries.OfType<Overlay>().Any();
    }

    // The overlays only: a palette replaced by another is never "off" in between.
    private static void TakeOff(ResourceDictionary resources)
    {
        foreach (ResourceDictionary overlay in resources.MergedDictionaries.OfType<Overlay>().ToList())
        {
            _ = resources.MergedDictionaries.Remove(overlay);
        }
    }

    private static object Value(Color colour, bool brush)
    {
        if (!brush)
        {
            return colour;
        }

        var solid = new SolidColorBrush(colour);
        solid.Freeze();
        return solid;
    }

    /// <summary>The overlay's own type, so it can be found and taken away again.</summary>
    private sealed class Overlay : ResourceDictionary;
}
