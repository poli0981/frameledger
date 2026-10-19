// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using FrameLedger.App.Charts;
using FrameLedger.App.Services;
using Wpf.Ui.Appearance;

namespace FrameLedger.App.Controls;

/// <summary>
/// The frame-generation mix as one bar (beta.24, owner decision D62): each segment a row's share of the session's presenting
/// time, in the chart palette's colours (<see cref="ChartPalette.MixFill"/>). It says nothing the table beside it does not —
/// every number is there, in text — and is announced by its <c>AutomationProperties.Name</c>.
/// </summary>
/// <remarks>
/// It follows a theme switch the way <see cref="ArtTint"/> does: a resource reference to <c>ApplicationBackgroundColor</c>, whose
/// change redraws it and whose luminance picks the palette — never <c>ApplicationThemeManager.Changed</c>, which only
/// <c>ApplicationThemeManager.Apply</c> raises. No colour is written in XAML (<c>08_UI</c> §Accessibility).
/// </remarks>
public sealed class FgMixBar : FrameworkElement
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments), typeof(IReadOnlyList<FgMixSegment>), typeof(FgMixBar),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The space between two segments, so neighbours of near colours — or any two in high contrast — still read as two.</summary>
    internal const double Gap = 2;

    // The theme, heard through a resource reference (ArtTint's way).
    private static readonly DependencyProperty _themeProperty = DependencyProperty.Register(
        "Theme", typeof(object), typeof(FgMixBar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public FgMixBar()
    {
        SetResourceReference(_themeProperty, "ApplicationBackgroundColor");
        SnapsToDevicePixels = true;
    }

    public IReadOnlyList<FgMixSegment>? Segments
    {
        get => (IReadOnlyList<FgMixSegment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    /// <summary>
    /// Each segment's rectangle in a bar <paramref name="width"/> by <paramref name="height"/>: widths in proportion to the
    /// shares, a <see cref="Gap"/> between two drawn segments; an empty rectangle (zero width) for a share of nothing.
    /// </summary>
    internal static IReadOnlyList<Rect> Layout(IReadOnlyList<FgMixSegment> segments, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var rects = new Rect[segments.Count];
        double total = segments.Sum(static s => Math.Max(0, s.Share));
        if (total <= 0 || width <= 0 || height <= 0)
        {
            return rects;
        }

        int drawn = segments.Count(static s => s.Share > 0);
        double usable = Math.Max(0, width - (Gap * (drawn - 1)));
        double x = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            double w = usable * Math.Max(0, segments[i].Share) / total;
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
    internal ChartPalette Palette() => GetValue(_themeProperty) is Color background
        ? ChartPalette.For(ColorMath.Luminance(background) < 0.5 ? ApplicationTheme.Dark : ApplicationTheme.Light)
        : ChartTheme.Current;

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (Segments is not { Count: > 0 } segments)
        {
            return;
        }

        ChartPalette palette = Palette();
        IReadOnlyList<Rect> rects = Layout(segments, ActualWidth, ActualHeight);
        for (int i = 0; i < rects.Count; i++)
        {
            if (rects[i].Width <= 0)
            {
                continue;
            }

            ScottPlot.Color fill = palette.MixFill(segments[i].Kind, segments[i].Factor);
            var brush = new SolidColorBrush(Color.FromArgb(fill.A, fill.R, fill.G, fill.B));
            brush.Freeze();
            drawingContext.DrawRoundedRectangle(brush, null, rects[i], 2, 2);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);
}
