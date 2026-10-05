// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Media;

namespace FrameLedger.App.Tests;

/// <summary>
/// WCAG 2.1 contrast arithmetic for the App's colour checks (<c>08_UI</c> §Contrast): a theme key resolved to a colour,
/// source-over compositing of a translucent colour on an opaque one, and the contrast ratio. Shared by
/// <see cref="ContrastTests"/> and the rendered checks since beta.14.
/// </summary>
internal static class Wcag
{
    /// <summary>WCAG AA for body text.</summary>
    public const double AA = 4.5;

    public static Color Resolve(ResourceDictionary themed, string key)
    {
        object value = themed[key] ?? System.Windows.Application.Current.Resources[key] ?? throw new InvalidOperationException($"{key} is in neither the theme nor the merged dictionaries");
        return value switch
        {
            Color c => c,
            SolidColorBrush b => b.Color,
            _ => throw new InvalidOperationException($"{key} is not a Color or SolidColorBrush in the theme dictionary (got {value.GetType().Name})"),
        };
    }

    /// <summary>Source-over compositing of <paramref name="top"/> on an opaque <paramref name="under"/>.</summary>
    public static Color Over(Color top, Color under)
    {
        double a = top.A / 255.0;
        return Color.FromRgb(
            (byte)Math.Round(top.R * a + under.R * (1 - a)),
            (byte)Math.Round(top.G * a + under.G * (1 - a)),
            (byte)Math.Round(top.B * a + under.B * (1 - a)));
    }

    /// <summary>WCAG 2.1 contrast ratio between two opaque colours.</summary>
    public static double Ratio(Color a, Color b)
    {
        double la = Luminance(a);
        double lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
    }
}
