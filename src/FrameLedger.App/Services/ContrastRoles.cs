// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Media;

namespace FrameLedger.App.Services;

/// <summary>
/// Which of a contrast theme's eight colours every WPF UI theme resource stands for (beta.35, D91), read from WPF UI 4.3.0's
/// own four high-contrast dictionaries (HC1 Dusk, HC2 Night sky, HCBlack Aquatic, HCWhite Desert).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not apply those dictionaries.</b> Measured 2026-11-09: each holds 459 keys, of which 84 of its 97 colours are a
/// <c>#FFFF0000</c> placeholder and 17 brushes (<c>SystemFillColor*Brush</c>, <c>MenuBar*</c>) throw when read; and their
/// colours are Windows 11's defaults, so a theme the user edited — or Windows 10's — would not be followed.
/// </para>
/// <para>
/// <b>What they are good for</b> is the role of each key: every brush there is one of the theme's eight colours (in HC1, 113
/// are the window colour, 68 the window text, 56 the highlight …). A key's role is the colour it has in all four — a key
/// that is the button face in one and the window in another is a button face — ties broken by its name, and a key with no
/// role colour anywhere (a placeholder, a throw) takes its brush twin's or its name's. <see cref="ContrastOverlay"/> then
/// gives every key the user's own colour for its role, and keeps the keys transparent there transparent.
/// </para>
/// <para>
/// <b>Only an opaque colour is evidence.</b> A brush that throws when its file is read alone does not throw in the App: its
/// forward reference falls back to the application's resources, and it reads as the Fluent theme's own fill — measured
/// 2026-11-09, <c>MenuBarItemBackgroundPressed</c> read <c>#0AFFFFFF</c> under the dark theme, the white of the window text
/// in three of the four, which made a pressed menu item white on white. A contrast theme has no translucent colour, so a
/// translucent one says nothing about a role, and the roles come out the same whichever Fluent theme lies under them.
/// </para>
/// </remarks>
public static class ContrastRoles
{
    /// <summary>WPF UI 4.3.0's high-contrast theme files (<c>Resources/Theme/*.xaml</c>).</summary>
    public static readonly IReadOnlyList<string> ThemeNames = ["HC1", "HC2", "HCBlack", "HCWhite"];

    private static readonly Color _placeholder = Color.FromArgb(0xFF, 0xFF, 0, 0);
    private static readonly Lazy<(IReadOnlyDictionary<string, (ContrastRole Role, bool Brush)> Roles, IReadOnlyDictionary<string, bool> Clear)> _inferred =
        new(static () => Infer([.. ThemeNames.Select(Load)]));

    /// <summary>Every key's role, and whether it is a brush (else a colour); keys transparent in a theme are in <see cref="Clear"/>.</summary>
    public static IReadOnlyDictionary<string, (ContrastRole Role, bool Brush)> Map => _inferred.Value.Roles;

    /// <summary>The keys transparent in one of the themes (true for a brush): the overlay keeps them transparent, so no colour of the Fluent theme under it shows through.</summary>
    public static IReadOnlyDictionary<string, bool> Clear => _inferred.Value.Clear;

    /// <summary>One of WPF UI's high-contrast dictionaries, read for its keys and colours — never applied.</summary>
    public static ResourceDictionary Load(string theme)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(theme);
        return new ResourceDictionary { Source = new Uri($"pack://application:,,,/Wpf.Ui;component/Resources/Theme/{theme}.xaml", UriKind.Absolute) };
    }

    /// <summary>The eight colours a high-contrast dictionary defines for itself (its <c>SystemColor*Color</c> keys).</summary>
    public static ContrastPalette PaletteOf(ResourceDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        return new ContrastPalette(
            Read(dictionary, "SystemColorWindowColor"), Read(dictionary, "SystemColorWindowTextColor"),
            Read(dictionary, "SystemColorHighlightColor"), Read(dictionary, "SystemColorHighlightTextColor"),
            Read(dictionary, "SystemColorHotlightColor"), Read(dictionary, "SystemColorGrayTextColor"),
            Read(dictionary, "SystemColorButtonFaceColor"), Read(dictionary, "SystemColorButtonTextColor"));
    }

    /// <summary>Every key's role across the dictionaries given (the four, in the App; a test's own, in a test), and the keys transparent in one.</summary>
    public static (IReadOnlyDictionary<string, (ContrastRole Role, bool Brush)> Roles, IReadOnlyDictionary<string, bool> Clear) Infer(IReadOnlyList<ResourceDictionary> dictionaries)
    {
        ArgumentNullException.ThrowIfNull(dictionaries);
        var seen = new Dictionary<string, List<HashSet<ContrastRole>>>(StringComparer.Ordinal);
        var brush = new Dictionary<string, bool>(StringComparer.Ordinal);
        var transparent = new HashSet<string>(StringComparer.Ordinal);
        foreach (ResourceDictionary dictionary in dictionaries)
        {
            ContrastPalette palette = PaletteOf(dictionary);
            foreach (string key in dictionary.Keys.OfType<string>())
            {
                object? value = TryRead(dictionary, key);
                brush[key] = brush.GetValueOrDefault(key) || value is not Color;
                Color? colour = value switch
                {
                    Color c => c,
                    SolidColorBrush b => b.Color,
                    _ => null,
                };
                if (colour is { A: 0 })
                {
                    _ = transparent.Add(key);
                    continue;
                }

                if (!seen.TryGetValue(key, out List<HashSet<ContrastRole>>? sets))
                {
                    sets = [];
                    seen[key] = sets;
                }

                // Opaque only: a translucent colour is a Fluent fill a forward reference fell back to, never a contrast colour.
                if (colour is { A: 0xFF } known && known != _placeholder)
                {
                    sets.Add([.. Enum.GetValues<ContrastRole>().Where(r => palette.Of(r) == Color.FromRgb(known.R, known.G, known.B))]);
                }
            }
        }

        var roles = new Dictionary<string, (ContrastRole, bool)>(StringComparer.Ordinal);
        foreach ((string key, List<HashSet<ContrastRole>> sets) in seen.Where(p => !transparent.Contains(p.Key)))
        {
            if (FromColours(key, sets) is { } role)
            {
                roles[key] = (role, brush[key]);
            }
        }

        // A colour key with no role colour anywhere (a placeholder) takes the role of its brush twin; then the name decides.
        foreach (string key in seen.Keys.Where(k => !transparent.Contains(k) && !roles.ContainsKey(k)))
        {
            roles[key] = (roles.TryGetValue(key + "Brush", out (ContrastRole Role, bool) twin) ? twin.Role : ByName(key), brush[key]);
        }

        return (roles, transparent.ToDictionary(static k => k, k => brush[k], StringComparer.Ordinal));
    }

    /// <summary>The role every dictionary agrees on, a tie broken by the name; null when no dictionary gave a role colour.</summary>
    private static ContrastRole? FromColours(string key, List<HashSet<ContrastRole>> sets)
    {
        List<HashSet<ContrastRole>> informative = [.. sets.Where(static s => s.Count > 0)];
        if (informative.Count == 0)
        {
            return null;
        }

        HashSet<ContrastRole> common = [.. informative[0]];
        foreach (HashSet<ContrastRole> set in informative.Skip(1))
        {
            common.IntersectWith(set);
        }

        HashSet<ContrastRole> choices = common.Count > 0 ? common : [.. informative.SelectMany(static s => s)];
        return choices.Count == 1 ? choices.First() : Prefer(key, choices);
    }

    /// <summary>Of several roles a colour fits, the one the key's name points at; a button's before a window's.</summary>
    private static ContrastRole Prefer(string key, HashSet<ContrastRole> choices)
    {
        bool button = key.Contains("Button", StringComparison.Ordinal);
        ContrastRole[] order = button
            ? [ContrastRole.ButtonFace, ContrastRole.ButtonText, ContrastRole.Window, ContrastRole.WindowText, ContrastRole.Highlight, ContrastRole.HighlightText, ContrastRole.HotTrack, ContrastRole.GrayText]
            : [ContrastRole.Window, ContrastRole.WindowText, ContrastRole.Highlight, ContrastRole.HighlightText, ContrastRole.HotTrack, ContrastRole.GrayText, ContrastRole.ButtonFace, ContrastRole.ButtonText];
        ContrastRole named = ByName(key);
        if (choices.Contains(named))
        {
            return named;
        }

        return order.First(choices.Contains);
    }

    /// <summary>A role from the key's name alone — for a placeholder, or a brush that would not even load.</summary>
    internal static ContrastRole ByName(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        bool Has(string part) => key.Contains(part, StringComparison.Ordinal);
        bool text = Has("TextFill") || Has("Foreground") || Has("Glyph") || (Has("Text") && !Has("Background"));
        if (Has("Disabled"))
        {
            return ContrastRole.GrayText;
        }

        if (Has("OnAccent") || (text && (Has("Selected") || Has("Pressed"))))
        {
            return ContrastRole.HighlightText;
        }

        if (Has("AccentText") || Has("Hyperlink") || Has("Hotlight"))
        {
            return ContrastRole.HotTrack;
        }

        if (Has("Accent") || ((Has("Background") || Has("Fill")) && (Has("Selected") || Has("Pressed"))))
        {
            return ContrastRole.Highlight;
        }

        if (text)
        {
            return Has("Button") ? ContrastRole.ButtonText : ContrastRole.WindowText;
        }

        if (Has("Stroke") || (Has("Border") && !Has("MenuBar")))
        {
            return ContrastRole.WindowText;
        }

        // A system fill with no Background in its name is an icon's or a mark's colour: the text colour.
        if (Has("SystemFillColor") && !Has("Background"))
        {
            return ContrastRole.WindowText;
        }

        return Has("Button") ? ContrastRole.ButtonFace : ContrastRole.Window;
    }

    private static object? TryRead(ResourceDictionary dictionary, string key)
    {
        try
        {
            return dictionary[key];
        }
        catch (Exception ex) when (ex is ResourceReferenceKeyNotFoundException or InvalidOperationException or System.Windows.Markup.XamlParseException)
        {
            return null;
        }
    }

    private static Color Read(ResourceDictionary dictionary, string key) =>
        TryRead(dictionary, key) is Color colour ? colour : throw new InvalidOperationException($"the high-contrast dictionary has no colour {key}");
}
