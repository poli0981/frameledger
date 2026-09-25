using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using FluentAssertions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;

namespace FrameLedger.App.Tests;

/// <summary>
/// <c>08_UI</c> §Contrast (2026-09-16). "Colours come from the theme dictionaries, so contrast cannot be broken locally"
/// had one hole: <c>ui:Badge Appearance="Info"</c> paints with WPF UI's <c>PaletteLightBlueBrush</c> — a fixed Material
/// colour from <c>Palette.xaml</c>, in neither theme dictionary — under the theme's text, and the Games card's "Hooking
/// on/off" read white on <c>#03A9F4</c> in dark, 2.6:1. No <c>#</c> in our XAML, so <c>NoXamlHardCodesAColour</c> could not
/// see it. This class holds the fix two ways: the four palette appearances are not used, and every pill pair the App
/// composes is ≥ 4.5:1 (WCAG AA) in BOTH theme dictionaries — with the old pair asserted below it, so the check is
/// known to bite.
/// </summary>
public sealed class ContrastTests
{
    private const double _aa = 4.5;

    private static readonly string[] _paletteAppearances = ["Info", "Caution", "Danger", "Success"];

    /// <summary>
    /// Windows' default accent. The accent brushes are not in the theme dictionaries: `ApplicationThemeManager.Apply(…,
    /// updateAccent: true)` derives them from the system accent at start-up (`MainWindow`), so the test derives them the
    /// same way from a fixed colour and measures WPF UI's own on-accent text against them.
    /// </summary>
    private static readonly Color _defaultAccent = Color.FromRgb(0x00, 0x78, 0xD4);

    [Fact]
    public void NoBadgeUsesAPaletteAppearance()
    {
        List<string> offenders = [];
        foreach ((string file, XDocument xaml) in AccessibilityTests.AppXaml())
        {
            foreach (XElement badge in xaml.Descendants().Where(static e => string.Equals(e.Name.LocalName, "Badge", StringComparison.Ordinal)))
            {
                string? appearance = badge.Attribute("Appearance")?.Value;
                if (appearance is not null && _paletteAppearances.Contains(appearance, StringComparer.Ordinal))
                {
                    offenders.Add($"{file}: ui:Badge Appearance=\"{appearance}\"");
                }
            }
        }

        offenders.Should().BeEmpty("Info/Caution/Danger/Success paint with Palette*Brush, a fixed Material colour outside the theme dictionaries (08_UI §Contrast)");
    }

    /// <summary>
    /// <c>Appearance="Secondary"</c> is refused too (2026-09-21). WPF UI 4.3.0's Secondary trigger repaints the badge's
    /// background only; Foreground stays <c>BadgeForeground</c>, the DEFAULT badge's on-accent colour, which over
    /// <c>ControlFillColorDefault</c> in the light theme is near-white on near-white: the Games card showed two empty boxes
    /// where the platform and the engine were. A literal attribute is what this can see; the Agent pill's bound
    /// appearance is <c>MainWindowViewModel</c>'s, and it never binds Secondary.
    /// </summary>
    [Fact]
    public void NoBadgeUsesTheSecondaryAppearance()
    {
        List<string> offenders = [];
        foreach ((string file, XDocument xaml) in AccessibilityTests.AppXaml())
        {
            foreach (XElement badge in xaml.Descendants().Where(static e => string.Equals(e.Name.LocalName, "Badge", StringComparison.Ordinal)))
            {
                if (string.Equals(badge.Attribute("Appearance")?.Value, "Secondary", StringComparison.Ordinal))
                {
                    offenders.Add($"{file}: ui:Badge Appearance=\"Secondary\"");
                }
            }
        }

        offenders.Should().BeEmpty("a Secondary badge keeps the default badge's foreground over a fill it was not chosen for; use Styles/FrameLedger.xaml's TagPill + TagPillText");
    }

    /// <summary>
    /// The pairs the App's own styles compose (<c>Styles/FrameLedger.xaml</c>): the hooking pill OFF (secondary text on the
    /// Pill's fill), ON (on-accent text on the accent fill, the ×FG chip's pair), and the warning qualifier (primary text
    /// on the caution fill) — also the hooking pill's "Anti-cheat" state since 2026-09-25. Translucent fills are composited
    /// over the card and the window, as they are on screen.
    /// </summary>
    [Theory]
    [InlineData(ApplicationTheme.Dark)]
    [InlineData(ApplicationTheme.Light)]
    public async Task EveryPillPairMeetsAAInBothThemes(ApplicationTheme theme)
    {
        Dictionary<string, double> ratios = await PagesLoadTests.OnStaAsync(() =>
        {
            var themed = new ThemesDictionary { Theme = theme };
            ApplicationAccentColorManager.Apply(_defaultAccent, theme, systemGlassColor: false, systemAccentColor: true);
            Color window = Resolve(themed, "ApplicationBackgroundColor");
            Color card = Over(Resolve(themed, "CardBackgroundFillColorDefault"), window);
            Color pillOff = Over(Resolve(themed, "ControlFillColorDefault"), card);
            Color accent = Over(Resolve(themed, "AccentFillColorDefaultBrush"), card);
            Color caution = Over(Resolve(themed, "SystemFillColorCautionBackground"), card);
            return new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["hooking off: secondary text on the pill"] = Ratio(Over(Resolve(themed, "TextFillColorSecondary"), pillOff), pillOff),
                ["hooking on: on-accent text on the accent"] = Ratio(Over(Resolve(themed, "TextOnAccentFillColorPrimaryBrush"), accent), accent),
                ["qualifier warning: primary text on caution"] = Ratio(Over(Resolve(themed, "TextFillColorPrimary"), caution), caution),
                ["the Info badge, for the record"] = Ratio(Over(Resolve(themed, "TextFillColorPrimary"), PaletteLightBlue()), PaletteLightBlue()),
            };
        });

        using (new FluentAssertions.Execution.AssertionScope(theme.ToString()))
        {
            ratios["hooking off: secondary text on the pill"].Should().BeGreaterThanOrEqualTo(_aa);
            ratios["hooking on: on-accent text on the accent"].Should().BeGreaterThanOrEqualTo(_aa);
            ratios["qualifier warning: primary text on caution"].Should().BeGreaterThanOrEqualTo(_aa);
        }

        if (theme == ApplicationTheme.Dark)
        {
            ratios["the Info badge, for the record"].Should().BeLessThan(_aa, "the pair this class exists for, white on #03A9F4, is what the check must be able to refuse");
        }
    }

    private static Color Resolve(ResourceDictionary themed, string key)
    {
        object value = themed[key] ?? System.Windows.Application.Current.Resources[key] ?? throw new InvalidOperationException($"{key} is in neither the theme nor the merged dictionaries");
        return value switch
        {
            Color c => c,
            SolidColorBrush b => b.Color,
            _ => throw new InvalidOperationException($"{key} is not a Color or SolidColorBrush in the theme dictionary (got {value.GetType().Name})"),
        };
    }

    /// <summary>WPF UI's Palette.xaml value, the same in both themes; read from the merged dictionaries rather than typed, so a palette change upstream is measured.</summary>
    private static Color PaletteLightBlue() => Resolve(new ResourceDictionary(), "PaletteLightBlueBrush");

    /// <summary>Source-over compositing of <paramref name="top"/> on an opaque <paramref name="under"/>.</summary>
    private static Color Over(Color top, Color under)
    {
        double a = top.A / 255.0;
        return Color.FromRgb(
            (byte)Math.Round(top.R * a + under.R * (1 - a)),
            (byte)Math.Round(top.G * a + under.G * (1 - a)),
            (byte)Math.Round(top.B * a + under.B * (1 - a)));
    }

    /// <summary>WCAG 2.1 contrast ratio between two opaque colours.</summary>
    private static double Ratio(Color a, Color b)
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
