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
    private static readonly string[] _fixedColourAppearances = ["Danger", "Info", "Success", "Caution", "Dark", "Light"];

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
    /// WPF UI 4.3.0 paints its Danger, Info, Success, Caution, Dark and Light buttons a fixed colour no theme resource reaches;
    /// the App's one destructive button goes through <c>DangerButton</c>, which is the theme's own button under a contrast theme.
    /// </summary>
    [Fact]
    public void NoButtonAppearanceThatPaintsAFixedColourIsSetOutsideTheDangerButtonStyle()
    {
        var offenders = new List<string>();
        foreach ((string file, XDocument xaml) in AccessibilityTests.AppXaml())
        {
            foreach (XElement button in xaml.Descendants().Where(static e => e.Name.LocalName.EndsWith("Button", StringComparison.Ordinal)))
            {
                if (button.Attribute("Appearance")?.Value is { } appearance && _fixedColourAppearances.Contains(appearance, StringComparer.Ordinal))
                {
                    offenders.Add($"{file}: {button.Name.LocalName} Appearance=\"{appearance}\"");
                }
            }

            foreach (XElement setter in xaml.Descendants().Where(static e => string.Equals(e.Name.LocalName, "Setter", StringComparison.Ordinal)
                                                                              && string.Equals(e.Attribute("Property")?.Value, "Appearance", StringComparison.Ordinal)))
            {
                string? style = setter.Ancestors().FirstOrDefault(static a => string.Equals(a.Name.LocalName, "Style", StringComparison.Ordinal))
                    ?.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value;
                if (setter.Attribute("Value")?.Value is { } value && _fixedColourAppearances.Contains(value, StringComparer.Ordinal) && !string.Equals(style, "DangerButton", StringComparison.Ordinal))
                {
                    offenders.Add($"{file}: a style sets Appearance=\"{value}\"");
                }
            }
        }

        offenders.Should().BeEmpty("a fixed colour is no contrast theme's colour; a destructive button uses Style=\"{StaticResource DangerButton}\"");
    }

    [Fact]
    public async Task TheRemoveButtonIsTheThemesOwnButtonWhileAContrastThemeIsOn()
    {
        // Under the dark theme so the accent the overlay writes is put back after.
        var facts = await PagesLoadTests.OnStaAsync(() => Visuals.UnderTheme(ApplicationTheme.Dark, () =>
        {
            ResourceDictionary resources = System.Windows.Application.Current.Resources;
            var button = new Wpf.Ui.Controls.Button { Content = "Remove…", Style = (Style)System.Windows.Application.Current.FindResource("DangerButton") };
            var host = new FluentWindow { Content = button, Width = 200, Height = 120, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
            host.Show();
            try
            {
                Visuals.Pump();
                ControlAppearance off = button.Appearance;
                ContrastOverlay.Install(resources, EditedDark);
                Visuals.Pump();
                ControlAppearance on = button.Appearance;
                Color face = ((SolidColorBrush)button.Background).Color;
                ContrastOverlay.Remove(resources);
                Visuals.Pump();
                return (off, on, face, Again: button.Appearance);
            }
            finally
            {
                ContrastOverlay.Remove(resources);
                host.Close();
            }
        }));

        facts.off.Should().Be(ControlAppearance.Danger);
        facts.on.Should().Be(ControlAppearance.Secondary, "turned on while the page is shown, the button changes at once");
        facts.face.Should().Be(EditedDark.ButtonFace);
        facts.Again.Should().Be(ControlAppearance.Danger);
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
