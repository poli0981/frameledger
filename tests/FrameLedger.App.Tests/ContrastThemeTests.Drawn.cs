// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.App.Controls;
using FrameLedger.App.Services;
using FrameLedger.Domain.Metrics;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Tests;

/// <summary>What the App draws itself under a contrast theme (beta.35, D91): the badges, the mix bar, the Remove button.</summary>
public sealed partial class ContrastThemeTests
{
    // Danger is not here: the App's implicit button style makes it read, and the theme's own button under a contrast theme.
    private static readonly string[] _fixedColourAppearances = ["Info", "Success", "Caution", "Dark", "Light"];

    /// <summary>
    /// The change badge and the frame-generation bar repaint when a contrast theme comes on even if its window colour is the
    /// Fluent theme's — Aquatic's <c>#202020</c> is the dark theme's own. They hear a theme through a resource reference to
    /// <c>ApplicationBackgroundColor</c>, and measured 2026-11-09 the overlay going on re-reads it and calls them though the
    /// colour is the same; this holds that, so no second signal is needed.
    /// </summary>
    [Fact]
    public async Task TheBadgeAndTheMixBarRepaintWhenAContrastThemeKeepsTheWindowColour()
    {
        var facts = await PagesLoadTests.OnStaAsync(() => Visuals.UnderTheme(ApplicationTheme.Dark, () =>
        {
            ResourceDictionary resources = System.Windows.Application.Current.Resources;
            ContrastPalette same = EditedDark with { Window = (Color)System.Windows.Application.Current.FindResource("ApplicationBackgroundColor") };
            var badge = new ChangeBadge { Text = "1" };
            var bar = new FgMixBar { Segments = [new FgMixSegment(FgMixKind.None, null, 1)], Width = 60, Height = 14 };
            var panel = new System.Windows.Controls.StackPanel();
            panel.Children.Add(badge);
            panel.Children.Add(bar);
            var host = new FluentWindow { Content = panel, Width = 200, Height = 120, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
            host.Show();
            try
            {
                Visuals.Pump();
                Color before = ((SolidColorBrush)badge.Background).Color;
                Color barBefore = Pixel(panel, bar);
                ContrastOverlay.Install(resources, same);
                try
                {
                    Visuals.Pump();
                    return (Before: before, During: ((SolidColorBrush)badge.Background).Color, Text: ((SolidColorBrush)badge.Foreground).Color,
                        BarBefore: barBefore, BarDuring: Pixel(panel, bar), Palette: same);
                }
                finally
                {
                    ContrastOverlay.Remove(resources);
                }
            }
            finally
            {
                host.Close();
            }
        }));

        facts.During.Should().NotBe(facts.Before, "the badge heard the contrast theme though the window colour stayed");
        facts.Palette.Colors.Should().Contain(facts.During).And.Contain(facts.Text);
        facts.BarDuring.Should().NotBe(facts.BarBefore, "and the bar redrew");
        facts.Palette.Colors.Should().Contain(facts.BarDuring);
    }

    /// <summary>
    /// WPF UI 4.3.0 paints its Info, Success, Caution, Dark and Light buttons a fixed colour under white text that no theme
    /// reaches, and a badge with a bound appearance escapes <c>ContrastTests</c>' look at the literal ones — the agent's pill was
    /// one, 2.16:1 in the light theme. Danger is allowed: the App's own button style makes it read (below).
    /// </summary>
    [Fact]
    public void NoButtonOrBadgeTakesAnAppearanceThatPaintsAFixedColour()
    {
        var offenders = new List<string>();
        foreach ((string file, XDocument xaml) in AccessibilityTests.AppXaml())
        {
            foreach (XElement element in xaml.Descendants())
            {
                string name = element.Name.LocalName;
                string? appearance = element.Attribute("Appearance")?.Value;
                if (appearance is null)
                {
                    continue;
                }

                if (name.EndsWith("Button", StringComparison.Ordinal) && _fixedColourAppearances.Contains(appearance, StringComparer.Ordinal))
                {
                    offenders.Add($"{file}: {name} Appearance=\"{appearance}\"");
                }

                if (string.Equals(name, "Badge", StringComparison.Ordinal) && appearance.StartsWith('{', StringComparison.Ordinal))
                {
                    offenders.Add($"{file}: a Badge whose appearance is bound, {appearance}");
                }
            }

            foreach (XElement setter in xaml.Descendants().Where(static e => string.Equals(e.Name.LocalName, "Setter", StringComparison.Ordinal)
                                                                              && string.Equals(e.Attribute("Property")?.Value, "Appearance", StringComparison.Ordinal)))
            {
                if (setter.Attribute("Value")?.Value is { } value && _fixedColourAppearances.Contains(value, StringComparer.Ordinal))
                {
                    offenders.Add($"{file}: a style sets Appearance=\"{value}\"");
                }
            }
        }

        offenders.Should().BeEmpty("a fixed colour under white text reads below 3:1 in a theme and is no contrast theme's colour");
    }

    /// <summary>
    /// A Danger button — the game page's Remove, the message boxes' Delete — reads in both themes (the App's implicit button
    /// style puts the theme's dark text on WPF UI's fixed red, where its own white read 3.68:1 in the dark theme) and is the
    /// theme's own button while a contrast theme is on, changing at once.
    /// </summary>
    [Fact]
    public async Task ADangerButtonReadsInBothThemesAndIsTheThemesOwnButtonUnderAContrastTheme()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            static (Color Face, Color Text) Drawn(Button b) => (((SolidColorBrush)b.Background).Color, ((SolidColorBrush)b.Foreground).Color);
            (Color Face, Color Text) Under(ApplicationTheme theme, Func<Button, (Color, Color)> read) => Visuals.UnderTheme(theme, () =>
            {
                var button = new Button { Content = "Remove…", Appearance = ControlAppearance.Danger };
                var host = new FluentWindow { Content = button, Width = 200, Height = 120, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
                host.Show();
                try
                {
                    Visuals.Pump();
                    return read(button);
                }
                finally
                {
                    ContrastOverlay.Remove(System.Windows.Application.Current.Resources);
                    host.Close();
                }
            });

            (Color Face, Color Text) dark = Under(ApplicationTheme.Dark, Drawn);
            (Color Face, Color Text) light = Under(ApplicationTheme.Light, Drawn);
            (Color, Color) on = default;
            (Color, Color) off = Under(ApplicationTheme.Dark, b =>
            {
                ContrastOverlay.Install(System.Windows.Application.Current.Resources, EditedDark);
                Visuals.Pump();
                on = Drawn(b);
                ContrastOverlay.Remove(System.Windows.Application.Current.Resources);
                Visuals.Pump();
                return Drawn(b);
            });
            return (dark, light, on, off);
        });

        ColorMath.Contrast(facts.dark.Text, facts.dark.Face).Should().BeGreaterThanOrEqualTo(4.5, "the dark theme's white read 3.68:1 on #F44336");
        ColorMath.Contrast(facts.light.Text, facts.light.Face).Should().BeGreaterThanOrEqualTo(4.5);
        facts.on.Should().Be((EditedDark.ButtonFace, EditedDark.ButtonText), "turned on while the button is shown, it is the theme's button at once");
        facts.off.Should().Be(facts.dark, "and turned off, the red one again");
    }

    /// <summary>
    /// The title bar's two marks read in every state, in both themes and under a contrast theme: the agent's state on the
    /// theme's own fills (<c>AgentPill</c>, which replaced a palette badge: 2.78, 2.63, 2.16 and 3.68:1 in the light theme)
    /// and the alpha build's badge, the accent under its own text.
    /// </summary>
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    [InlineData("HC1")]
    [InlineData("HCWhite")]
    [InlineData("EditedLight")]
    public async Task TheTitleBarsMarksReadInEveryState(string name)
    {
        ControlAppearance[] states = [ControlAppearance.Success, ControlAppearance.Info, ControlAppearance.Caution, ControlAppearance.Danger];
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            ContrastPalette? palette = name is "Dark" or "Light" ? null : Named(name);
            List<(string Mark, Color Text, Color Under)> Measure()
            {
                var panel = new System.Windows.Controls.StackPanel();
                var marks = new List<(string Mark, System.Windows.Controls.TextBlock Text)>();
                foreach (ControlAppearance state in states)
                {
                    var text = new System.Windows.Controls.TextBlock { Text = state.ToString() };
                    text.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
                    panel.Children.Add(new System.Windows.Controls.Border
                    {
                        Style = (Style)System.Windows.Application.Current.FindResource("AgentPill"),
                        DataContext = new { AgentStatusAppearance = state },
                        Child = text,
                    });
                    marks.Add(("agent " + state, text));
                }

                var alpha = new Badge { Appearance = ControlAppearance.Primary, Content = "Alpha" };
                panel.Children.Add(alpha);
                var host = new FluentWindow { Content = panel, Width = 300, Height = 300, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
                host.Show();
                try
                {
                    Visuals.Pump();
                    marks.Add(("alpha badge", Visuals.FindAll<System.Windows.Controls.TextBlock>(alpha).Single()));
                    Color window = (Color)System.Windows.Application.Current.FindResource("ApplicationBackgroundColor");
                    return [.. marks.Select(m =>
                    {
                        Color under = Visuals.PaintedUnder(m.Text, panel, window);
                        return (m.Mark, Visuals.TextOver(m.Text, under), under);
                    })];
                }
                finally
                {
                    host.Close();
                }
            }

            if (palette is null)
            {
                return Visuals.UnderTheme(Enum.Parse<ApplicationTheme>(name), Measure);
            }

            List<(string Mark, Color Text, Color Under)> drawn = Visuals.UnderContrast(palette, Measure);
            return [.. drawn.Select(m => (palette.Colors.Contains(m.Text) && palette.Colors.Contains(m.Under) ? m.Mark : m.Mark + " (not the theme's colours)", m.Text, m.Under))];
        });

        facts.Should().HaveCount(5);
        facts.Where(static m => ColorMath.Contrast(m.Text, m.Under) < 4.5 || m.Mark.EndsWith(')', StringComparison.Ordinal)).Select(static m => $"{m.Mark}: {m.Text} on {m.Under}, {ColorMath.Contrast(m.Text, m.Under):0.00}:1")
            .Should().BeEmpty($"every mark in the title bar reads at 4.5:1 under {name}");
    }

    [Fact]
    public async Task TheContrastThemeSaysWhenItComesAndGoes()
    {
        // Heard on the STA thread inside the one work item: the other tests' contrast themes come and go on it too.
        (List<string> heard, bool on) = await PagesLoadTests.OnStaAsync(() =>
        {
            var said = new List<string>();
            void Heard(object? sender, PropertyChangedEventArgs e) => said.Add(e.PropertyName!);
            ContrastTheme.StaticPropertyChanged += Heard;
            try
            {
                var resources = new ResourceDictionary();
                ContrastOverlay.Install(resources, EditedDark);
                ContrastOverlay.Install(resources, EditedDark);
                ContrastOverlay.Install(resources, EditedLight);
                ContrastOverlay.Remove(resources);
                return (said, ContrastTheme.IsOn);
            }
            finally
            {
                ContrastTheme.StaticPropertyChanged -= Heard;
            }
        });

        heard.Should().Equal(["Active", "IsOn", "Active", "Active", "IsOn"], "on, another palette while on, off — the same palette twice says nothing, and a palette replaced is never off between");
        on.Should().BeFalse();
    }

    [Fact]
    public async Task UnderAContrastThemeAChartBadgesTextIsTheThemesColourThatReadsBest()
    {
        var facts = await PagesLoadTests.OnStaAsync(() =>
        {
            var resources = new ResourceDictionary();
            ContrastOverlay.Install(resources, EditedLight);
            try
            {
                return (OnHotTrack: ChartTheme.ToWpf(ChartTheme.TextOn(ChartTheme.ToScottPlot(EditedLight.HotTrack))),
                    OnWindow: ChartTheme.ToWpf(ChartTheme.TextOn(ChartTheme.ToScottPlot(EditedLight.Window))));
            }
            finally
            {
                ContrastOverlay.Remove(resources);
            }
        });

        facts.OnHotTrack.Should().Be(EditedLight.Window, "white reads best on the edited theme's dark blue link colour, and white is its window");
        facts.OnWindow.Should().Be(EditedLight.WindowText);
    }

    /// <summary>The colour drawn at the middle of <paramref name="element"/>, rendered as part of <paramref name="root"/>.</summary>
    private static Color Pixel(FrameworkElement root, FrameworkElement element)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        Point middle = element.TranslatePoint(new Point(element.ActualWidth / 2, element.ActualHeight / 2), root);
        byte[] pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect((int)middle.X, (int)middle.Y, 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }
}
