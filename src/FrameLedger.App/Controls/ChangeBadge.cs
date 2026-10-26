// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FrameLedger.App.Charts;

namespace FrameLedger.App.Controls;

/// <summary>
/// A change marker's number in the list under a trend chart (beta.30, owner decision D70): the same orange and the same text
/// colour as the chart's badge (<see cref="ChartTheme.TextOn"/>), so a row reads as that badge's. Not focusable — the row's
/// text carries the meaning.
/// </summary>
/// <remarks>
/// It follows a theme switch the way <see cref="FgMixBar"/> does: a resource reference to <c>ApplicationBackgroundColor</c>,
/// whose change repaints it and whose luminance picks the palette. No colour is written in XAML (<c>08_UI</c> §Accessibility).
/// </remarks>
public sealed class ChangeBadge : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(ChangeBadge), new PropertyMetadata(string.Empty));

    // The theme, heard through a resource reference (FgMixBar's way).
    private static readonly DependencyProperty _themeProperty = DependencyProperty.Register(
        "Theme", typeof(object), typeof(ChangeBadge), new PropertyMetadata(null, static (d, _) => ((ChangeBadge)d).Repaint()));

    static ChangeBadge() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ChangeBadge), new FrameworkPropertyMetadata(typeof(ChangeBadge)));

    public ChangeBadge()
    {
        SetResourceReference(_themeProperty, "ApplicationBackgroundColor");
        Repaint();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>The palette for the theme the element's resources hold now; the applied theme's when none is reachable.</summary>
    internal ChartPalette Palette() => GetValue(_themeProperty) is Color background ? ChartPalette.ForBackground(background) : ChartTheme.Current;

    private void Repaint()
    {
        ScottPlot.Color fill = Palette().StutterPso;
        Background = Frozen(ChartTheme.ToWpf(fill));
        Foreground = Frozen(ChartTheme.ToWpf(ChartTheme.TextOn(fill)));
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
