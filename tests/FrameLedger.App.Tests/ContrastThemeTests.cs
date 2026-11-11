// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.App.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;

namespace FrameLedger.App.Tests;

/// <summary>
/// Windows contrast themes, followed (beta.35, owner decision D91): every WPF UI theme resource gets the user's colour for
/// its role, read from WPF UI's own four high-contrast dictionaries; the App's own colours — the charts', the bar's, the
/// badges' — come from the same eight; a theme choice gives way to a contrast theme while it is on. A test cannot turn a
/// contrast theme on in Windows, so the palettes are WPF UI's four defaults and two the user might have edited.
/// </summary>
public sealed partial class ContrastThemeTests
{
    /// <summary>A dark contrast theme with every colour edited — the button face no longer the window's.</summary>
    internal static readonly ContrastPalette EditedDark = new(Color.FromRgb(0x10, 0x10, 0x18), Color.FromRgb(0xFF, 0xFF, 0x00), Color.FromRgb(0x1A, 0xEB, 0xFF),
        Color.FromRgb(0x00, 0x00, 0x00), Color.FromRgb(0x75, 0xE1, 0xFF), Color.FromRgb(0x3F, 0xF2, 0x3F), Color.FromRgb(0x20, 0x20, 0x40), Color.FromRgb(0xFF, 0xFF, 0xFF));

    /// <summary>A light one, edited the same way.</summary>
    internal static readonly ContrastPalette EditedLight = new(Color.FromRgb(0xFF, 0xFF, 0xFF), Color.FromRgb(0x10, 0x10, 0x10), Color.FromRgb(0x37, 0x00, 0x6E),
        Color.FromRgb(0xFF, 0xFF, 0xFF), Color.FromRgb(0x00, 0x00, 0x9F), Color.FromRgb(0x60, 0x00, 0x00), Color.FromRgb(0xF0, 0xF0, 0xE0), Color.FromRgb(0x20, 0x20, 0x20));

    private static readonly Color _red = Color.FromArgb(0xFF, 0xFF, 0, 0);

    private static readonly string[] _fluentThemes = ["Dark", "Light"];

    [GeneratedRegex(@"(?:DynamicResource|StaticResource)\s+(?<key>[A-Za-z]\w+)\}|(?:SetResourceReference\([^,]+,\s*|FindResource\(|TryFindResource\()""(?<key>[A-Za-z]\w+)""", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, 1000)]
    private static partial Regex ResourceKey();

    /// <summary>A named palette for a theory: WPF UI's four defaults by file name, and the two edited ones.</summary>
    internal static ContrastPalette Named(string name) => name switch
    {
        "EditedDark" => EditedDark,
        "EditedLight" => EditedLight,
        _ => ContrastRoles.PaletteOf(ContrastRoles.Load(name)),
    };

    /// <summary>
    /// The reason the App does not apply WPF UI's high-contrast dictionaries, pinned: the theme name has no file, and each
    /// file's colours are mostly placeholders. When WPF UI fixes them this goes red, and the overlay may not be needed.
    /// </summary>
    [Fact]
    public async Task WpfUisHighContrastThemeIsStillPlaceholdersAndAMissingFile()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            string missing;
            try
            {
                _ = new ThemesDictionary { Theme = ApplicationTheme.HighContrast };
                missing = "loaded";
            }
            catch (Exception ex) when (ex is IOException or System.Windows.Markup.XamlParseException or InvalidOperationException)
            {
                missing = ex.GetType().Name;
            }

            int red = 0;
            foreach (string theme in ContrastRoles.ThemeNames)
            {
                ResourceDictionary d = ContrastRoles.Load(theme);
                red += d.Keys.OfType<string>().Count(k => TryRead(d, k) is Color c && c == _red);
            }

            return (missing, red);
        });

        facts.missing.Should().NotBe("loaded", "WPF UI 4.3.0 names a HighContrast theme it has no dictionary for");
        facts.red.Should().BeGreaterThan(200, "84 of 97 colours of each high-contrast dictionary are a red placeholder (2026-11-09)");
    }

    [Fact]
    public async Task EveryKeyOfWpfUisContrastThemesHasARoleAndThePlaceholdersTooAndNoneIsRed()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            IReadOnlyDictionary<string, (ContrastRole Role, bool Brush)> roles = ContrastRoles.Map;
            var missing = new List<string>();
            foreach (string theme in ContrastRoles.ThemeNames)
            {
                ResourceDictionary d = ContrastRoles.Load(theme);
                missing.AddRange(d.Keys.OfType<string>().Where(k => !roles.ContainsKey(k) && !ContrastRoles.Clear.ContainsKey(k)).Select(k => theme + ": " + k));
            }

            ResourceDictionary overlay = ContrastOverlay.Build(EditedDark);
            string[] red = [.. overlay.Keys.OfType<string>().Where(k => ColourOf(overlay[k]) == _red)];
            return (roles.Count, Missing: missing.Distinct(StringComparer.Ordinal).ToList(), Red: red, Clear: ContrastRoles.Clear.Keys.ToList(),
                Pressed: roles["MenuBarItemBackgroundPressed"].Role, PressedText: roles["MenuBarItemTextForegroundPressed"].Role,
                Critical: roles["SystemFillColorCriticalBrush"].Role, Text: roles["TextFillColorSecondary"].Role);
        });

        facts.Count.Should().BeGreaterThan(400);
        facts.Missing.Should().BeEmpty("every key stands for one of the theme's colours or is transparent");
        facts.Red.Should().BeEmpty("no placeholder survives the overlay");
        facts.Clear.Should().Contain(["MenuBarBackground", "ControlFillColorTransparentBrush", "SubtleFillColorTransparentBrush"]);
        facts.Pressed.Should().Be(ContrastRole.Highlight, "a brush WPF UI's file cannot load alone, read in the App as a translucent Fluent fill, takes its role from its name");
        facts.PressedText.Should().Be(ContrastRole.HighlightText, "so a pressed menu item is the highlight's text on the highlight, not white on white");
        facts.Critical.Should().Be(ContrastRole.WindowText, "a system fill with no Background in its name is an icon's colour");
        facts.Text.Should().Be(ContrastRole.WindowText, "a placeholder colour takes its brush twin's role");
    }

    /// <summary>
    /// The roles do not depend on the Fluent theme lying under the App when they are first read: the brushes that throw when
    /// WPF UI's file is read alone fall back to the application's resources — the Fluent theme's fills — inside the App.
    /// </summary>
    [Fact]
    public async Task TheRolesAreTheSameWhicheverFluentThemeLiesUnderThem()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            static (IReadOnlyDictionary<string, (ContrastRole Role, bool Brush)> Roles, IReadOnlyDictionary<string, bool> Clear) Read() =>
                ContrastRoles.Infer([.. ContrastRoles.ThemeNames.Select(ContrastRoles.Load)]);
            var dark = Visuals.UnderTheme(ApplicationTheme.Dark, Read);
            var light = Visuals.UnderTheme(ApplicationTheme.Light, Read);
            return (Dark: dark.Roles.OrderBy(static p => p.Key, StringComparer.Ordinal).Select(static p => $"{p.Key}={p.Value}").ToList(),
                Light: light.Roles.OrderBy(static p => p.Key, StringComparer.Ordinal).Select(static p => $"{p.Key}={p.Value}").ToList(),
                DarkClear: dark.Clear.Keys.Order(StringComparer.Ordinal).ToList(), LightClear: light.Clear.Keys.Order(StringComparer.Ordinal).ToList());
        });

        facts.Light.Should().Equal(facts.Dark);
        facts.LightClear.Should().Equal(facts.DarkClear);
    }

    /// <summary>No colour of a Fluent theme shows through: every colour key the light and the dark themes have, the overlay has.</summary>
    [Fact]
    public async Task EveryColourOfTheFluentThemesIsOneTheOverlayGives()
    {
        List<string> uncovered = await PagesLoadTests.OnStaAsync(() =>
        {
            ResourceDictionary overlay = ContrastOverlay.Build(EditedDark);
            var found = new List<string>();
            foreach (string theme in _fluentThemes)
            {
                var d = new ResourceDictionary { Source = new Uri($"pack://application:,,,/Wpf.Ui;component/Resources/Theme/{theme}.xaml", UriKind.Absolute) };
                found.AddRange(d.Keys.OfType<string>().Where(k => TryRead(d, k) is Color or Brush && !overlay.Contains(k) && !ContrastOverlay.AccentKeys.ContainsKey(k)).Select(k => theme + ": " + k));
            }

            return found;
        });

        uncovered.Should().BeEmpty("a key the Fluent themes colour and no contrast dictionary has goes in ContrastOverlay.FluentOnlyKeys with its role");
    }

    /// <summary>
    /// With each default theme's own colours, the overlay gives every key the colour WPF UI's file gives it — but the text on
    /// the accent, which the App departs from on purpose (<see cref="TheTextOnTheAccentIsTheHighlightsText"/>).
    /// </summary>
    [Fact]
    public async Task OnEachDefaultThemeTheOverlayGivesEveryKeyTheColourWpfUiGivesIt()
    {
        List<string> different = await PagesLoadTests.OnStaAsync(() =>
        {
            var found = new List<string>();
            foreach (string theme in ContrastRoles.ThemeNames)
            {
                ResourceDictionary d = ContrastRoles.Load(theme);
                ContrastPalette palette = ContrastRoles.PaletteOf(d);
                ResourceDictionary overlay = ContrastOverlay.Build(palette);
                foreach (string key in overlay.Keys.OfType<string>().Where(static k => !ContrastOverlay.OnAccentKeys.ContainsKey(k)))
                {
                    if (ColourOf(TryRead(d, key)) is { A: 0xFF } wpfUi && palette.Colors.Contains(wpfUi) && ColourOf(overlay[key]) is { } ours
                        && Color.FromRgb(ours.R, ours.G, ours.B) != Color.FromRgb(wpfUi.R, wpfUi.G, wpfUi.B))
                    {
                        found.Add($"{theme}: {key} is {ours} in the overlay, {wpfUi} in WPF UI's file");
                    }
                }
            }

            return found;
        });

        string.Join(Environment.NewLine, different).Should().BeEmpty("on Windows' own default contrast themes the overlay is WPF UI's colours, key for key");
    }

    /// <summary>
    /// The chips the App draws on the accent — the frame-generation factor, RT/PT/RR, the upscaler — read under a contrast
    /// theme: the accent is the highlight there, so its text is the highlight's text, where WPF UI's files say the window's.
    /// </summary>
    [Fact]
    public async Task TheTextOnTheAccentIsTheHighlightsText()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            ResourceDictionary hc1 = ContrastRoles.Load("HC1");
            ContrastPalette palette = ContrastRoles.PaletteOf(hc1);
            ResourceDictionary overlay = ContrastOverlay.Build(palette);
            return (Ours: ColourOf(overlay["TextOnAccentFillColorPrimaryBrush"]), WpfUi: ColourOf(TryRead(hc1, "TextOnAccentFillColorPrimaryBrush")), palette.HighlightText, palette.WindowText,
                Ratio: ColorMath.Contrast(palette.HighlightText, palette.Highlight), WpfUiRatio: ColorMath.Contrast(palette.WindowText, palette.Highlight));
        });

        facts.Ours.Should().Be(facts.HighlightText);
        facts.WpfUi.Should().Be(facts.WindowText, "WPF UI 4.3.0's contrast files put the window text on the accent");
        facts.Ratio.Should().BeGreaterThanOrEqualTo(4.5);
        facts.WpfUiRatio.Should().BeLessThan(3, "white on Dusk's highlight, 1.62:1 — what the chips read before");
    }

    [Theory]
    [InlineData("EditedDark")]
    [InlineData("EditedLight")]
    public async Task AnEditedThemesColoursReachEveryKey(string name)
    {
        List<string> foreign = await PagesLoadTests.OnStaAsync(() =>
        {
            ContrastPalette palette = Named(name);
            ResourceDictionary overlay = ContrastOverlay.Build(palette);
            return overlay.Keys.OfType<string>().Where(k => ColourOf(overlay[k]) is not { } c || !(palette.Colors.Contains(c) || (c.A == 0 && ContrastRoles.Clear.ContainsKey(k)))).ToList();
        });

        foreign.Should().BeEmpty("every colour of the overlay is one of the user's eight, or transparent where WPF UI's contrast themes are");
    }

    [Fact]
    public async Task InstallPutsTheColoursAndTheAccentOnAndRemoveTakesThemOff()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            var resources = new ResourceDictionary();
            ContrastOverlay.Install(resources, EditedDark);
            var on = (Installed: ContrastOverlay.IsInstalled(resources), ContrastTheme.Active, Accent: ColourOf(resources["AccentFillColorDefaultBrush"]),
                OnAccent: ColourOf(resources["TextOnAccentFillColorPrimary"]), Merged: resources.MergedDictionaries.Count);
            ContrastOverlay.Install(resources, EditedLight);
            int again = resources.MergedDictionaries.Count;
            ContrastOverlay.Remove(resources);
            return (on, again, Off: ContrastOverlay.IsInstalled(resources), ActiveOff: ContrastTheme.Active);
        });

        facts.on.Installed.Should().BeTrue();
        facts.on.Active.Should().Be(EditedDark);
        facts.on.Accent.Should().Be(EditedDark.Highlight, "the accent the theme manager writes into the application's resources becomes the highlight");
        facts.on.OnAccent.Should().Be(EditedDark.HighlightText);
        facts.again.Should().Be(1, "installing again replaces the overlay");
        facts.Off.Should().BeFalse();
        facts.ActiveOff.Should().BeNull();
    }

    /// <summary>Every resource the App's XAML and code read resolves under a contrast theme, and to no placeholder.</summary>
    [Theory]
    [InlineData("HC1")]
    [InlineData("HCWhite")]
    [InlineData("EditedDark")]
    public async Task EveryResourceTheAppReadsResolvesUnderAContrastTheme(string name)
    {
        string app = Path.Combine(AccessibilityTests.RepoRoot(), "src", "FrameLedger.App");
        HashSet<string> keys = new(Directory.EnumerateFiles(app, "*.*", SearchOption.AllDirectories)
            .Where(static f => (f.EndsWith(".xaml", StringComparison.Ordinal) || f.EndsWith(".cs", StringComparison.Ordinal))
                               && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .SelectMany(static f => ResourceKey().Matches(File.ReadAllText(f)).Select(static m => m.Groups["key"].Value)), StringComparer.Ordinal);

        List<string> broken = await PagesLoadTests.OnStaAsync(() => Visuals.UnderContrast(Named(name), () =>
            keys.Where(k => System.Windows.Application.Current.TryFindResource(k) is var v && (v is null || ColourOf(v) == _red)).Order(StringComparer.Ordinal).ToList()));

        keys.Should().HaveCountGreaterThan(40);
        string.Join(", ", broken).Should().BeEmpty("a resource the App reads either resolves under a contrast theme or is not one the App reads");
    }

    [Fact]
    public void AContrastThemeWinsOverTheChoiceAndTheChoiceComesBack()
    {
        ThemePlan.For(AppTheme.Light, contrastOn: true, contrastDark: true, systemDark: false)
            .Should().Be(new ThemePlan(ApplicationTheme.Dark, WindowBackdropType.None, WatchSystem: false, Contrast: true), "a dark contrast theme lies over the dark Fluent theme, whatever the choice");
        ThemePlan.For(AppTheme.Dark, contrastOn: true, contrastDark: false, systemDark: true).Theme.Should().Be(ApplicationTheme.Light);
        ThemePlan.For(AppTheme.System, contrastOn: true, contrastDark: false, systemDark: true).WatchSystem.Should().BeFalse("WPF UI's watcher would apply its own high-contrast files");
        ThemePlan.For(AppTheme.System, contrastOn: false, contrastDark: false, systemDark: true)
            .Should().Be(new ThemePlan(ApplicationTheme.Dark, WindowBackdropType.Mica, WatchSystem: true, Contrast: false));
        ThemePlan.For(AppTheme.Light, contrastOn: false, contrastDark: true, systemDark: true)
            .Should().Be(new ThemePlan(ApplicationTheme.Light, WindowBackdropType.Mica, WatchSystem: false, Contrast: false), "off again, the choice applies");
        FluentActions.Invoking(static () => ThemePlan.For((AppTheme)99, false, false, false)).Should().Throw<ArgumentOutOfRangeException>();
    }

    private sealed class FakeContrast : IContrastSource
    {
        public event EventHandler? Changed;

        public bool IsOn { get; set; }

        public ContrastPalette Palette { get; set; } = EditedDark;

        public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The applier follows a contrast theme on and off as Windows reports it, whatever Settings ▸ Theme says.</summary>
    [Fact]
    public async Task TheApplierFollowsAContrastThemeOnAndOffAtOnce()
    {
        var facts = await PagesLoadTests.OnStaAsync(() => Visuals.UnderTheme(ApplicationTheme.Dark, () =>
        {
            var source = new FakeContrast { IsOn = true, Palette = EditedLight };
            using var applier = new WpfThemeApplier(source);
            ResourceDictionary resources = System.Windows.Application.Current.Resources;
            applier.Apply(AppTheme.Dark, null);
            bool on = ContrastOverlay.IsInstalled(resources);
            ApplicationTheme under = ApplicationThemeManager.GetAppTheme();
            ChartPalette chart = ChartTheme.Current;
            source.IsOn = false;
            source.Raise();
            Visuals.Pump();
            return (on, under, chart.Figure, Off: ContrastOverlay.IsInstalled(resources), ContrastTheme.Active, After: ApplicationThemeManager.GetAppTheme());
        }));

        facts.on.Should().BeTrue();
        facts.under.Should().Be(ApplicationTheme.Light, "a light contrast theme lies over the light Fluent theme, though the choice is Dark");
        facts.Figure.Should().Be(ChartTheme.ToScottPlot(EditedLight.Window), "the charts take the contrast theme's window colour");
        facts.Off.Should().BeFalse("turned off in Windows, the overlay comes off at once");
        facts.Active.Should().BeNull();
        facts.After.Should().Be(ApplicationTheme.Dark, "and the choice applies again");
    }

    [Theory]
    [InlineData("HC1")]
    [InlineData("EditedLight")]
    public async Task UnderAContrastThemeEveryChartColourIsOneOfTheThemes(string name)
    {
        ContrastPalette palette = await PagesLoadTests.OnStaAsync(() => Named(name));
        ChartPalette chart = ChartPalette.ForContrast(palette);
        HashSet<Color> allowed = [.. palette.Colors];

        ChartPalette.Keys.Select(k => (Key: k, Colour: ChartTheme.ToWpf(chart.ByKey(k)))).Where(p => !allowed.Contains(p.Colour)).Select(static p => p.Key)
            .Should().BeEmpty("under a contrast theme a chart is drawn in the theme's colours only");
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

    private static Color? ColourOf(object? value) => value switch
    {
        Color c => c,
        SolidColorBrush b => b.Color,
        _ => null,
    };
}
