// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Media;

namespace FrameLedger.App.Services;

/// <summary>
/// A Windows contrast theme's colours (beta.35, D91): the eight the user sees — and may edit — in Settings ▸ Accessibility ▸
/// Contrast themes. <see cref="FromSystem"/> reads the ones Windows has on now; a test builds its own.
/// </summary>
public sealed record ContrastPalette(Color Window, Color WindowText, Color Highlight, Color HighlightText, Color HotTrack, Color GrayText, Color ButtonFace, Color ButtonText)
{
    /// <summary>The theme Windows has on now — the user's colours, edited or not, on Windows 10 and 11 alike.</summary>
    public static ContrastPalette FromSystem() => new(
        SystemColors.WindowColor, SystemColors.WindowTextColor, SystemColors.HighlightColor, SystemColors.HighlightTextColor,
        SystemColors.HotTrackColor, SystemColors.GrayTextColor, SystemColors.ControlColor, SystemColors.ControlTextColor);

    /// <summary>A dark window (Aquatic, Dusk, Night sky) rather than a light one (Desert): which Fluent theme lies under it.</summary>
    public bool IsDark => ColorMath.Luminance(Window) < 0.5;

    /// <summary>The colour of a role, fully opaque — a contrast theme has no translucent colours.</summary>
    public Color Of(ContrastRole role)
    {
        Color c = role switch
        {
            ContrastRole.Window => Window,
            ContrastRole.WindowText => WindowText,
            ContrastRole.Highlight => Highlight,
            ContrastRole.HighlightText => HighlightText,
            ContrastRole.HotTrack => HotTrack,
            ContrastRole.GrayText => GrayText,
            ContrastRole.ButtonFace => ButtonFace,
            ContrastRole.ButtonText => ButtonText,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "not a contrast role"),
        };
        return Color.FromRgb(c.R, c.G, c.B);
    }

    /// <summary>Every colour of the palette, in role order.</summary>
    public IReadOnlyList<Color> Colors => [.. Enum.GetValues<ContrastRole>().Select(Of)];
}
