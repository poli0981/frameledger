// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentAssertions;
using FrameLedger.App.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;

namespace FrameLedger.App.Tests;

/// <summary>
/// Every text the App shows is readable in both themes (beta.18, the owner's screenshots of beta.17 in the dark theme: each
/// page's title and section headings near-black on the dark window). Each surface is hosted as the App hosts it
/// (<see cref="Hosted"/>): a page in WPF UI's <c>NavigationViewContentPresenter</c>, which is a <c>Frame</c>, and a Frame
/// sets <c>InheritanceBehavior.SkipToAppNow</c> — nothing above it, the window's text colour included, reaches the page. A
/// text with no colour of its own then falls back to the TextBlock default, which WPF UI 4.3.0's <c>TextBlockMetadata</c>
/// sets to black. Text inside a card or a grid never showed it: their templates set a colour. The theme-switch check could
/// not see it either: it compares a switched surface with a fresh one, and black was black in both.
/// </summary>
public sealed partial class PagesLoadTests
{
    /// <summary>WCAG's minimum for large text and for what a user must make out; black on the dark window is 1.3.</summary>
    private const double _readableAtLeast = 3.0;

    [Theory]
    [InlineData(ApplicationTheme.Dark)]
    [InlineData(ApplicationTheme.Light)]
    public async Task EveryTextIsReadableInBothThemes(ApplicationTheme theme)
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        using Surfaces surfaces = await Surfaces.LoadAsync(s);

        (int Texts, List<string> Faint) result = await OnStaAsync(() => Visuals.UnderTheme(theme, () =>
        {
            Color window = Wcag.Resolve(new ThemesDictionary { Theme = theme }, "ApplicationBackgroundColor");
            int texts = 0;
            var faint = new List<string>();
            foreach ((string name, Func<FrameworkElement> build) in surfaces.All())
            {
                (Window host, FrameworkElement root) = Hosted(build);
                try
                {
                    Lay(root);
                    foreach (TextBlock text in ShownTexts(root))
                    {
                        texts++;
                        Color under = Visuals.PaintedUnder(text, root, window);
                        double ratio = Wcag.Ratio(Visuals.TextOver(text, under), under);
                        if (ratio < _readableAtLeast)
                        {
                            faint.Add(string.Create(CultureInfo.InvariantCulture, $"{name}: \"{text.Text}\" {ratio:0.00}:1 ({text.Foreground} on {under})"));
                        }
                    }
                }
                finally
                {
                    host.Close();
                }
            }

            return (texts, faint);
        }));

        result.Texts.Should().BeGreaterThan(300, "every surface was rendered and holds text");
        string.Join(Environment.NewLine, result.Faint).Should().BeEmpty($"every text the App shows reads at {_readableAtLeast}:1 or better in the {theme} theme");
    }

    /// <summary>
    /// Under a Windows contrast theme (beta.35, owner decision D91) every text is drawn in one of the theme's own colours over
    /// one of them — nothing of the Fluent theme under it, nothing made translucent — and reads at 4.5:1 or better: WPF UI's
    /// four default themes, and two the user edited (<see cref="ContrastThemeTests.EditedDark"/>, <see cref="ContrastThemeTests.EditedLight"/>).
    /// </summary>
    [Theory]
    [InlineData("HC1")]
    [InlineData("HC2")]
    [InlineData("HCBlack")]
    [InlineData("HCWhite")]
    [InlineData("EditedDark")]
    [InlineData("EditedLight")]
    public async Task EveryTextIsInTheContrastThemesColoursAndReadable(string name)
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        using Surfaces surfaces = await Surfaces.LoadAsync(s);

        (int Texts, List<string> Wrong) result = await OnStaAsync(() =>
        {
            ContrastPalette palette = ContrastThemeTests.Named(name);
            return Visuals.UnderContrast(palette, () =>
            {
                HashSet<Color> colours = [.. palette.Colors];
                int texts = 0;
                var wrong = new List<string>();
                foreach ((string surface, Func<FrameworkElement> build) in surfaces.All())
                {
                    (Window host, FrameworkElement root) = Hosted(build);
                    try
                    {
                        Lay(root);
                        foreach (TextBlock text in ShownTexts(root))
                        {
                            texts++;
                            Color under = Visuals.PaintedUnder(text, root, palette.Window);
                            Color drawn = Visuals.TextOver(text, under);
                            double ratio = Wcag.Ratio(drawn, under);
                            if (!colours.Contains(drawn) || !colours.Contains(under) || ratio < Wcag.AA)
                            {
                                wrong.Add(string.Create(CultureInfo.InvariantCulture, $"{surface}: \"{text.Text}\" {drawn} on {under}, {ratio:0.00}:1"));
                            }
                        }
                    }
                    finally
                    {
                        host.Close();
                    }
                }

                return (texts, wrong);
            });
        });

        result.Texts.Should().BeGreaterThan(300, "every surface was rendered and holds text");
        string.Join(Environment.NewLine, result.Wrong).Should().BeEmpty($"under the {name} contrast theme every text is the theme's colour on the theme's colour, at {Wcag.AA}:1 or better");
    }

    /// <summary>
    /// The line under Settings ▸ Theme says a contrast theme's colours are used while one is on, and goes when it is off — on
    /// the page as it is shown, without visiting it again (beta.35, D91).
    /// </summary>
    [Fact]
    public async Task TheSettingsPageSaysAContrastThemeIsOnWhileItIs()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        using Surfaces surfaces = await Surfaces.LoadAsync(s);

        var facts = await OnStaAsync(() => Visuals.UnderTheme(ApplicationTheme.Dark, () =>
        {
            Func<FrameworkElement> build = surfaces.All().Single(static p => string.Equals(p.Item1, "Settings", StringComparison.Ordinal)).Item2;
            (Window host, FrameworkElement root) = Hosted(build);
            try
            {
                Lay(root);
                TextBlock note = Visuals.FindAll<TextBlock>(root).Single(static t => string.Equals(t.Text, Strings.Settings_Theme_ContrastOn, StringComparison.Ordinal));
                Visibility off = note.Visibility;
                ResourceDictionary resources = System.Windows.Application.Current.Resources;
                ContrastOverlay.Install(resources, ContrastThemeTests.EditedDark);
                Visibility on;
                try
                {
                    Visuals.Pump();
                    on = note.Visibility;
                }
                finally
                {
                    ContrastOverlay.Remove(resources);
                }

                Visuals.Pump();
                return (off, on, Again: note.Visibility);
            }
            finally
            {
                host.Close();
            }
        }));

        facts.off.Should().Be(Visibility.Collapsed);
        facts.on.Should().Be(Visibility.Visible);
        facts.Again.Should().Be(Visibility.Collapsed);
    }

    /// <summary>
    /// The non-empty texts under <paramref name="root"/> that a user can see and is meant to read: no collapsed or hidden
    /// element above them, and none disabled (WCAG asks nothing of an inactive control's text).
    /// </summary>
    private static IEnumerable<TextBlock> ShownTexts(DependencyObject root)
    {
        if (root is UIElement { Visibility: not Visibility.Visible } or UIElement { IsEnabled: false })
        {
            yield break;
        }

        if (root is TextBlock { Text.Length: > 0 } text && text.Foreground is SolidColorBrush)
        {
            yield return text;
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            foreach (TextBlock child in ShownTexts(VisualTreeHelper.GetChild(root, i)))
            {
                yield return child;
            }
        }
    }
}
