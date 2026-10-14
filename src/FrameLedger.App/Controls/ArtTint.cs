// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FrameLedger.App.Services;

namespace FrameLedger.App.Controls;

/// <summary>
/// The colour of the game page's artwork band (beta.23, owner decision D61), set on its <see cref="Border"/>: nothing at all
/// while the game shows no artwork — the page is as it was —, the card's own fill when it shows some untinted (the option is
/// off, the picture has no colour, the system's high-contrast mode is on), and otherwise the picture's dominant colour over
/// that fill at the strongest opacity (at most 30 % on a dark theme, 22 % on a light one) at which every text the header
/// shows still reads — primary and secondary text 4.5:1, tertiary 3:1, the pills' and the warning's text 4.5:1 on their
/// fills (<see cref="ColorMath.Tint"/>).
/// </summary>
/// <remarks>
/// The second colour the App works out rather than takes from the theme, after the chart palettes (<c>08_UI</c> §Contrast).
/// It follows a theme switch through a resource reference on the band itself — a private attached property bound to
/// <c>ApplicationBackgroundColor</c>, whose change recomputes it — never through <c>ApplicationThemeManager.Changed</c>:
/// only <c>ApplicationThemeManager.Apply</c> raises that, and the tests switch themes by replacing the theme dictionary.
/// </remarks>
public static class ArtTint
{
    public static readonly DependencyProperty ShowsArtProperty = DependencyProperty.RegisterAttached(
        "ShowsArt", typeof(bool), typeof(ArtTint), new PropertyMetadata(false, OnChanged));

    public static readonly DependencyProperty ColorProperty = DependencyProperty.RegisterAttached(
        "Color", typeof(Color?), typeof(ArtTint), new PropertyMetadata(null, OnChanged));

    // The theme, heard through a resource reference on the band (set the first time either property above changes).
    private static readonly DependencyProperty _themeProperty = DependencyProperty.RegisterAttached(
        "Theme", typeof(object), typeof(ArtTint), new PropertyMetadata(null, OnChanged));

    private static readonly string[] _keys = ["ApplicationBackgroundColor", "CardBackgroundFillColorDefault", "TextFillColorPrimary",
        "TextFillColorSecondary", "TextFillColorTertiary", "ControlFillColorDefault", "SystemFillColorCautionBackground"];

    /// <summary>The banner's fade into the band: opaque for its upper half, transparent at its foot — alpha only.</summary>
    public static Brush Fade { get; } = CreateFade();

    public static bool GetShowsArt(DependencyObject element) => (bool)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(ShowsArtProperty);

    public static void SetShowsArt(DependencyObject element, bool value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(ShowsArtProperty, value);

    public static Color? GetColor(DependencyObject element) => (Color?)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(ColorProperty);

    public static void SetColor(DependencyObject element, Color? value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(ColorProperty, value);

    /// <summary>The band's colour for the theme its resources hold now, or null for the card's own fill.</summary>
    internal static Color? BandColour(FrameworkElement element, Color dominant)
    {
        ArgumentNullException.ThrowIfNull(element);
        Color?[] c = [.. _keys.Select(k => ColourOf(element.TryFindResource(k)))];
        if (c.Any(static x => x is null))
        {
            return null;
        }

        Color background = ColorMath.Over(c[1]!.Value, c[0]!.Value);
        ColorMath.Pair[] pairs =
        [
            new(c[2]!.Value, null, 4.5),
            new(c[3]!.Value, null, 4.5),
            new(c[4]!.Value, null, 3.0),
            new(c[3]!.Value, c[5], 4.5),
            new(c[2]!.Value, c[6], 4.5),
        ];

        // A dark theme's text is light: the band may take more colour before the light text stops reading.
        bool dark = ColorMath.Luminance(background) < 0.18;
        return ColorMath.Tint(background, dominant, pairs, dark ? 0.30 : 0.22);
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Border band)
        {
            return;
        }

        if (e.Property != _themeProperty && band.ReadLocalValue(_themeProperty) == DependencyProperty.UnsetValue)
        {
            band.SetResourceReference(_themeProperty, "ApplicationBackgroundColor");
        }

        Apply(band);
    }

    private static void Apply(Border band)
    {
        if (!GetShowsArt(band))
        {
            band.ClearValue(Border.BackgroundProperty);
            return;
        }

        if (GetColor(band) is { } dominant && !SystemParameters.HighContrast && BandColour(band, dominant) is { } colour)
        {
            var brush = new SolidColorBrush(colour);
            brush.Freeze();
            band.Background = brush;
            return;
        }

        band.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
    }

    private static LinearGradientBrush CreateFade()
    {
        var fade = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        fade.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0, 0, 0), 0));
        fade.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0, 0, 0), 0.55));
        fade.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1));
        fade.Freeze();
        return fade;
    }

    private static Color? ColourOf(object? resource) => resource switch
    {
        Color colour => colour,
        SolidColorBrush brush => brush.Color,
        _ => null,
    };
}
