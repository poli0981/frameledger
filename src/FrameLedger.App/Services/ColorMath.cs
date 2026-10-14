// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;
using System.Windows.Media;

namespace FrameLedger.App.Services;

/// <summary>
/// The arithmetic of the game page's artwork tint (beta.23, owner decision D61): WCAG 2.1's relative luminance and contrast
/// ratio, source-over compositing, an artwork's dominant colour, and the strongest tint that keeps every text pair the
/// header shows readable. The tests check it with their own, separate copy of the WCAG formulas (<c>Wcag</c>), so a mistake
/// here cannot also be the check's.
/// </summary>
internal static class ColorMath
{
    /// <summary>A text pair the tint must keep readable: the text's colour, an optional fill under it (a pill), and the ratio it needs.</summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct Pair(Color Text, Color? Fill, double Minimum);

    /// <summary>WCAG 2.1 relative luminance of an opaque colour.</summary>
    public static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
    }

    /// <summary>WCAG 2.1 contrast ratio between two opaque colours.</summary>
    public static double Contrast(Color a, Color b)
    {
        double la = Luminance(a);
        double lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary>Source-over: <paramref name="top"/>, with its own alpha, on an opaque <paramref name="under"/>.</summary>
    public static Color Over(Color top, Color under)
    {
        double a = top.A / 255.0;
        return Color.FromRgb(
            (byte)Math.Round((top.R * a) + (under.R * (1 - a))),
            (byte)Math.Round((top.G * a) + (under.G * (1 - a))),
            (byte)Math.Round((top.B * a) + (under.B * (1 - a))));
    }

    /// <summary><paramref name="colour"/> laid over <paramref name="under"/> at <paramref name="alpha"/> (0–1), opaque.</summary>
    public static Color Mix(Color under, Color colour, double alpha) =>
        Over(Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), colour.R, colour.G, colour.B), under);

    /// <summary>
    /// The colour an artwork reads as, from its pixels (BGRA, 8 bits each): the hue that carries the most of its vivid pixels —
    /// each weighted by its saturation and brightness, near-black and near-white ones not counted — averaged over the pixels of
    /// that hue and its two neighbours. Null for an image with nothing vivid in it (grey, black and white, a dark poster):
    /// such an image tints nothing.
    /// </summary>
    public static Color? Dominant(ReadOnlySpan<byte> bgra)
    {
        const int hues = 36;
        Span<double> weight = stackalloc double[hues];
        Span<double> r = stackalloc double[hues];
        Span<double> g = stackalloc double[hues];
        Span<double> b = stackalloc double[hues];
        double total = 0;
        int pixels = 0;
        for (int i = 0; i + 3 < bgra.Length; i += 4)
        {
            if (bgra[i + 3] < 128)
            {
                continue;   // transparent: not part of the picture
            }

            pixels++;
            (double h, double s, double v) = Hsv(bgra[i + 2], bgra[i + 1], bgra[i]);
            if (v < 0.15 || s < 0.15)
            {
                continue;   // near-black, or grey and near-white: no colour to give
            }

            double w = s * v;
            int bin = Math.Min(hues - 1, (int)(h / 360.0 * hues));
            weight[bin] += w;
            r[bin] += bgra[i + 2] * w;
            g[bin] += bgra[i + 1] * w;
            b[bin] += bgra[i] * w;
            total += w;
        }

        // Fewer than a twentieth of the picture's pixels' worth of colour: it is a grey picture with a few coloured specks.
        return pixels == 0 || total < pixels * 0.05 ? null : AverageAroundHeaviest(weight, r, g, b);
    }

    /// <summary>The weighted average colour of the heaviest hue and its two neighbours.</summary>
    private static Color AverageAroundHeaviest(ReadOnlySpan<double> weight, ReadOnlySpan<double> r, ReadOnlySpan<double> g, ReadOnlySpan<double> b)
    {
        int hues = weight.Length;
        int best = 0;
        double bestWeight = -1;
        for (int bin = 0; bin < hues; bin++)
        {
            double around = weight[bin] + weight[(bin + 1) % hues] + weight[(bin + hues - 1) % hues];
            if (around > bestWeight)
            {
                bestWeight = around;
                best = bin;
            }
        }

        double sw = 0;
        double sr = 0;
        double sg = 0;
        double sb = 0;
        foreach (int bin in new[] { (best + hues - 1) % hues, best, (best + 1) % hues })
        {
            sw += weight[bin];
            sr += r[bin];
            sg += g[bin];
            sb += b[bin];
        }

        return Color.FromRgb((byte)Math.Round(sr / sw), (byte)Math.Round(sg / sw), (byte)Math.Round(sb / sw));
    }

    /// <summary>
    /// The band's colour: <paramref name="dominant"/> over <paramref name="background"/> at the largest opacity, at most
    /// <paramref name="maxAlpha"/>, at which every pair in <paramref name="pairs"/> still reads at its minimum — tried from
    /// <paramref name="maxAlpha"/> down in steps of one hundredth, because the luminance of a mix need not move one way as it
    /// deepens. The background itself when even the faintest tint would cost a pair its contrast.
    /// </summary>
    public static Color Tint(Color background, Color dominant, IReadOnlyList<Pair> pairs, double maxAlpha)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        for (int step = (int)Math.Round(Math.Clamp(maxAlpha, 0, 1) * 100); step > 0; step--)
        {
            Color band = Mix(background, dominant, step / 100.0);
            if (pairs.All(p => Reads(p, band)))
            {
                return band;
            }
        }

        return background;
    }

    /// <summary>Whether <paramref name="pair"/> reads at its minimum on <paramref name="band"/>.</summary>
    public static bool Reads(Pair pair, Color band)
    {
        Color under = pair.Fill is { } fill ? Over(fill, band) : band;
        return Contrast(Over(pair.Text, under), under) >= pair.Minimum;
    }

    private static (double H, double S, double V) Hsv(byte red, byte green, byte blue)
    {
        double r = red / 255.0;
        double g = green / 255.0;
        double b = blue / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        double h = delta == 0 ? 0
            : max == r ? 60 * (((g - b) / delta) % 6)
            : max == g ? 60 * (((b - r) / delta) + 2)
            : 60 * (((r - g) / delta) + 4);
        if (h < 0)
        {
            h += 360;
        }

        return (h, max == 0 ? 0 : delta / max, max);
    }
}
