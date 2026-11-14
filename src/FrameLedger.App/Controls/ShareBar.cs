// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using FrameLedger.App.Charts;

namespace FrameLedger.App.Controls;

/// <summary>
/// Shares of one whole as one bar: each segment as wide as its share, in the chart palette's colours. What
/// <see cref="FgMixBar"/> (beta.24, D62) and <see cref="InputShareBar"/> (0.1.0-beta.50) have in common. A bar says
/// nothing the table beside it does not - every number is there, in text - and is announced by its
/// <c>AutomationProperties.Name</c>.
/// </summary>
/// <remarks>
/// It follows a theme switch the way <see cref="ArtTint"/> does: a resource reference to <c>ApplicationBackgroundColor</c>, whose
/// change redraws it and whose luminance picks the palette — never <c>ApplicationThemeManager.Changed</c>, which only
/// <c>ApplicationThemeManager.Apply</c> raises. No colour is written in XAML (<c>08_UI</c> §Accessibility).
/// </remarks>
public abstract class ShareBar : FrameworkElement
{
    /// <summary>The space between two segments, so neighbours of near colours — or any two in high contrast — still read as two.</summary>
    internal const double Gap = 2;

    // The theme, heard through a resource reference (ArtTint's way).
    private static readonly DependencyProperty _themeProperty = DependencyProperty.Register(
        "Theme", typeof(object), typeof(ShareBar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    protected ShareBar()
    {
        SetResourceReference(_themeProperty, "ApplicationBackgroundColor");
        SnapsToDevicePixels = true;
    }

    /// <summary>The shares to draw, in order; empty when there is nothing to draw.</summary>
    protected abstract IReadOnlyList<double> Shares { get; }

    /// <summary>The colour of the segment at <paramref name="index"/>.</summary>
    protected abstract ScottPlot.Color Fill(ChartPalette palette, int index);

    /// <summary>
    /// Each share's rectangle in a bar <paramref name="width"/> by <paramref name="height"/>: widths in proportion to the
    /// shares, a <see cref="Gap"/> between two drawn segments; an empty rectangle (zero width) for a share of nothing.
    /// </summary>
    internal static IReadOnlyList<Rect> Layout(IReadOnlyList<double> shares, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(shares);
        var rects = new Rect[shares.Count];
        double total = shares.Sum(static s => Math.Max(0, s));
        if (total <= 0 || width <= 0 || height <= 0)
        {
            return rects;
        }

        int drawn = shares.Count(static s => s > 0);
        double usable = Math.Max(0, width - (Gap * (drawn - 1)));
        double x = 0;
        for (int i = 0; i < shares.Count; i++)
        {
            double w = usable * Math.Max(0, shares[i]) / total;
            if (w <= 0)
            {
                continue;
            }

            rects[i] = new Rect(x, 0, w, height);
            x += w + Gap;
        }

        return rects;
    }

    /// <summary>The palette for the theme the element's resources hold now; the applied theme's when none is reachable.</summary>
    internal ChartPalette Palette() => GetValue(_themeProperty) is Color background ? ChartPalette.ForBackground(background) : ChartTheme.Current;

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        IReadOnlyList<double> shares = Shares;
        if (shares.Count == 0)
        {
            return;
        }

        ChartPalette palette = Palette();
        IReadOnlyList<Rect> rects = Layout(shares, ActualWidth, ActualHeight);
        for (int i = 0; i < rects.Count; i++)
        {
            if (rects[i].Width <= 0)
            {
                continue;
            }

            ScottPlot.Color fill = Fill(palette, i);
            var brush = new SolidColorBrush(Color.FromArgb(fill.A, fill.R, fill.G, fill.B));
            brush.Freeze();
            drawingContext.DrawRoundedRectangle(brush, null, rects[i], 2, 2);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);
}
