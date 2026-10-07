// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

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
    /// beta.17 (the owner's screenshots of 2026-10-07: Settings' descriptions light grey on white after Dark → Light, dark grey
    /// on dark after the way back). WPF UI 4.3.0's <c>ui:TextBlock Appearance</c> looks its brush up ONCE —
    /// <c>OnAppearanceChanged</c> stores <c>TryFindResource(…)</c> and <c>TextBlockMetadata</c> coerces Foreground to it — so a
    /// text keeps the theme it was made under. The property inherits, so the attached form on a container pins every text
    /// below it, and a style setter or the code form does the same. Colour comes from <c>Styles/FrameLedger.xaml</c>'s
    /// <c>SecondaryText</c> / <c>TertiaryText</c>, whose <c>DynamicResource</c> follows a switch.
    /// </summary>
    [Fact]
    public void NoTextBlockTakesItsColourFromAppearance()
    {
        List<string> offenders = [];
        foreach ((string file, XDocument xaml) in AccessibilityTests.AppXaml())
        {
            foreach (XElement element in xaml.Descendants())
            {
                if (TakesItsColourFromAppearance(element))
                {
                    offenders.Add($"{file}:{((System.Xml.IXmlLineInfo)element).LineNumber}: <{element.Name.LocalName}>");
                }
            }
        }

        string app = System.IO.Path.Combine(AccessibilityTests.RepoRoot(), "src", "FrameLedger.App");
        foreach (string file in System.IO.Directory.EnumerateFiles(app, "*.cs", System.IO.SearchOption.AllDirectories)
                     .Where(static f => !f.Contains(System.IO.Path.DirectorySeparatorChar + "obj" + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal)
                                        && !f.Contains(System.IO.Path.DirectorySeparatorChar + "bin" + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal)))
        {
            string code = System.IO.File.ReadAllText(file);
            if (code.Contains("TextColor.", StringComparison.Ordinal) || code.Contains("TextBlock.AppearanceProperty", StringComparison.Ordinal))
            {
                offenders.Add(System.IO.Path.GetRelativePath(app, file) + ": the code form");
            }
        }

        string.Join(Environment.NewLine, offenders).Should().BeEmpty("WPF UI 4.3.0's TextBlock Appearance keeps the brush of the theme a text was made under (08_UI §Contrast); use Style=\"{StaticResource SecondaryText}\" or TertiaryText");
    }

    /// <summary>An <c>Appearance</c> on a TextBlock, the attached <c>ui:TextBlock.Appearance</c> on anything, or a style setter for either.</summary>
    private static bool TakesItsColourFromAppearance(XElement element)
    {
        bool isTextBlock = string.Equals(element.Name.LocalName, "TextBlock", StringComparison.Ordinal);
        if (element.Attributes().Any(a => (isTextBlock && string.Equals(a.Name.LocalName, "Appearance", StringComparison.Ordinal))
                                          || a.Name.LocalName.EndsWith("TextBlock.Appearance", StringComparison.Ordinal)))
        {
            return true;
        }

        if (!string.Equals(element.Name.LocalName, "Setter", StringComparison.Ordinal) || element.Attribute("Property")?.Value is not { } property)
        {
            return false;
        }

        string? target = element.Ancestors().FirstOrDefault(static a => string.Equals(a.Name.LocalName, "Style", StringComparison.Ordinal))?.Attribute("TargetType")?.Value;
        return property.EndsWith("TextBlock.Appearance", StringComparison.Ordinal)
               || (string.Equals(property, "Appearance", StringComparison.Ordinal) && target is not null && target.Contains("TextBlock", StringComparison.Ordinal));
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
            Color window = Wcag.Resolve(themed, "ApplicationBackgroundColor");
            Color card = Wcag.Over(Wcag.Resolve(themed, "CardBackgroundFillColorDefault"), window);
            Color pillOff = Wcag.Over(Wcag.Resolve(themed, "ControlFillColorDefault"), card);
            Color accent = Wcag.Over(Wcag.Resolve(themed, "AccentFillColorDefaultBrush"), card);
            Color caution = Wcag.Over(Wcag.Resolve(themed, "SystemFillColorCautionBackground"), card);
            return new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["hooking off: secondary text on the pill"] = Wcag.Ratio(Wcag.Over(Wcag.Resolve(themed, "TextFillColorSecondary"), pillOff), pillOff),
                ["hooking on: on-accent text on the accent"] = Wcag.Ratio(Wcag.Over(Wcag.Resolve(themed, "TextOnAccentFillColorPrimaryBrush"), accent), accent),
                ["qualifier warning: primary text on caution"] = Wcag.Ratio(Wcag.Over(Wcag.Resolve(themed, "TextFillColorPrimary"), caution), caution),
                ["the Info badge, for the record"] = Wcag.Ratio(Wcag.Over(Wcag.Resolve(themed, "TextFillColorPrimary"), PaletteLightBlue()), PaletteLightBlue()),
            };
        });

        using (new FluentAssertions.Execution.AssertionScope(theme.ToString()))
        {
            ratios["hooking off: secondary text on the pill"].Should().BeGreaterThanOrEqualTo(Wcag.AA);
            ratios["hooking on: on-accent text on the accent"].Should().BeGreaterThanOrEqualTo(Wcag.AA);
            ratios["qualifier warning: primary text on caution"].Should().BeGreaterThanOrEqualTo(Wcag.AA);
        }

        if (theme == ApplicationTheme.Dark)
        {
            ratios["the Info badge, for the record"].Should().BeLessThan(Wcag.AA, "the pair this class exists for, white on #03A9F4, is what the check must be able to refuse");
        }
    }

    /// <summary>WPF UI's Palette.xaml value, the same in both themes; read from the merged dictionaries rather than typed, so a palette change upstream is measured.</summary>
    private static Color PaletteLightBlue() => Wcag.Resolve(new ResourceDictionary(), "PaletteLightBlueBrush");
}
