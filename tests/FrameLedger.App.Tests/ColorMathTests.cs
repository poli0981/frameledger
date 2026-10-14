// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentAssertions;
using FrameLedger.App.Controls;
using FrameLedger.App.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.23 (owner decision D61): an artwork's dominant colour and the band's tint, checked against the tests' own copy of
/// the WCAG formulas (<see cref="Wcag"/>) — for the colours that are hardest on each theme's text — so the tint can never
/// cost a text its contrast.
/// </summary>
public sealed class ColorMathTests
{
    private static byte[] Pixels(params (Color Colour, int Count)[] runs)
    {
        var bytes = new List<byte>();
        foreach ((Color c, int count) in runs)
        {
            for (int i = 0; i < count; i++)
            {
                bytes.AddRange([c.B, c.G, c.R, c.A]);
            }
        }

        return [.. bytes];
    }

    [Fact]
    public void TheArithmeticAgreesWithTheTestsOwnWcag()
    {
        Color[] samples = [Colors.White, Colors.Black, Color.FromRgb(0x20, 0x20, 0x20), Color.FromRgb(0xF3, 0xF3, 0xF3), Color.FromRgb(0xC8, 0x28, 0x28), Color.FromRgb(0x28, 0xC8, 0x64)];
        foreach (Color a in samples)
        {
            foreach (Color b in samples)
            {
                ColorMath.Contrast(a, b).Should().BeApproximately(Wcag.Ratio(a, b), 1e-9);
            }
        }

        ColorMath.Over(Color.FromArgb(0x80, 0xFF, 0x00, 0x00), Colors.Black).Should().Be(Wcag.Over(Color.FromArgb(0x80, 0xFF, 0x00, 0x00), Colors.Black));
    }

    [Fact]
    public void TheDominantColourIsTheVividHueTheGreyAndBlackAroundItGiveNothing()
    {
        Color red = Color.FromRgb(200, 30, 40);
        Color blue = Color.FromRgb(30, 60, 210);
        ColorMath.Dominant(Pixels((red, 100))).Should().Be(red);
        ColorMath.Dominant(Pixels((Colors.White, 60), (Colors.Black, 20), (blue, 20)))!.Value.Should().Be(blue, "white and black carry no colour");
        ColorMath.Dominant(Pixels((Color.FromRgb(120, 120, 120), 99), (red, 1))).Should().BeNull("a grey picture with a speck of colour tints nothing");
        ColorMath.Dominant(Pixels((Color.FromArgb(0, 255, 0, 0), 50))).Should().BeNull("transparent pixels are not the picture");
    }

    /// <summary>
    /// The hardest colours for each theme: saturated yellow and white lift the dark theme's background toward its light text;
    /// black, deep blue and red pull the light theme's toward its dark text. Every text pair the header shows keeps its floor.
    /// </summary>
    [Theory]
    [InlineData(ApplicationTheme.Dark)]
    [InlineData(ApplicationTheme.Light)]
    public async Task TheTintKeepsEveryTextOnTheBandReadableForTheHardestColours(ApplicationTheme theme)
    {
        Color[] hardest = [Colors.White, Colors.Black, Color.FromRgb(255, 230, 0), Color.FromRgb(0, 30, 200), Color.FromRgb(220, 0, 0), Color.FromRgb(0, 200, 120), Color.FromRgb(128, 128, 128)];

        List<string> faint = await PagesLoadTests.OnStaAsync(() =>
        {
            var themed = new ThemesDictionary { Theme = theme };
            var band = new Border();
            band.Resources.MergedDictionaries.Add(themed);
            Color window = Wcag.Resolve(themed, "ApplicationBackgroundColor");
            Color card = Wcag.Over(Wcag.Resolve(themed, "CardBackgroundFillColorDefault"), window);
            var found = new List<string>();
            foreach (Color dominant in hardest)
            {
                Color tinted = ArtTint.BandColour(band, dominant)!.Value;
                foreach ((string text, string? fill, double floor) in new[]
                {
                    ("TextFillColorPrimary", (string?)null, 4.5), ("TextFillColorSecondary", null, 4.5), ("TextFillColorTertiary", null, 3.0),
                    ("TextFillColorSecondary", "ControlFillColorDefault", 4.5), ("TextFillColorPrimary", "SystemFillColorCautionBackground", 4.5),
                })
                {
                    Color under = fill is null ? tinted : Wcag.Over(Wcag.Resolve(themed, fill), tinted);
                    double ratio = Wcag.Ratio(Wcag.Over(Wcag.Resolve(themed, text), under), under);
                    if (ratio < floor)
                    {
                        found.Add($"{dominant} {text}/{fill}: {ratio:0.00} < {floor}");
                    }
                }

                // The tint never goes past its cap: at most 30 % (dark) or 22 % (light) of the colour over the card.
                double mix = Math.Max(Math.Abs(tinted.R - card.R), Math.Max(Math.Abs(tinted.G - card.G), Math.Abs(tinted.B - card.B)));
                double reach = Math.Max(Math.Abs(dominant.R - card.R), Math.Max(Math.Abs(dominant.G - card.G), Math.Abs(dominant.B - card.B)));
                if (reach > 0 && mix / reach > (theme == ApplicationTheme.Dark ? 0.30 : 0.22) + 0.01)
                {
                    found.Add($"{dominant}: {mix / reach:0.00} of the colour");
                }
            }

            return found;
        });

        faint.Should().BeEmpty($"every text on the tinted band keeps its floor in the {theme} theme");
    }

    [Fact]
    public async Task ATintIsColourWhereTheThemeLeavesRoomForIt()
    {
        Color tinted = await PagesLoadTests.OnStaAsync(() =>
        {
            var band = new Border();
            band.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = ApplicationTheme.Dark });
            return ArtTint.BandColour(band, Color.FromRgb(200, 30, 40))!.Value;
        });

        tinted.R.Should().BeGreaterThan((byte)(tinted.G + 20), "a red cover reddens the dark band — it is a tint, not the card's fill");
    }
}
