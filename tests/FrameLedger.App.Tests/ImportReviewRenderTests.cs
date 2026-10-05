// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Import;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;

namespace FrameLedger.App.Tests;

/// <summary>
/// The import checklist as the owner saw it (beta.14, a screenshot in the dark theme): every row white with white text, the
/// tick boxes cut to slivers at the left edge, and a list scrolling inside a dialog that scrolled too, which carried the
/// intro out of sight. Each check renders the dialog itself (<see cref="ImportReviewPrompt.Create"/>) laid out at a
/// window's size under the App's dictionaries — <c>PagesLoadTests</c>' check counted the rows and looked at nothing.
/// </summary>
public sealed class ImportReviewRenderTests
{
    private static ImportReviewViewModel Candidates(int count)
    {
        var list = new List<ImportCandidate>();
        for (int i = 0; i < count; i++)
        {
            string dir = string.Create(CultureInfo.InvariantCulture, $@"C:\Games\Game {i}");
            string? exe = i == 1 ? null : string.Create(CultureInfo.InvariantCulture, $@"{dir}\game{i}.exe");
            list.Add(new ImportCandidate(new StoreGame("steam", i.ToString(CultureInfo.InvariantCulture), "Game " + i.ToString(CultureInfo.InvariantCulture), dir, exe, null),
                exe, AlreadyInLibrary: i == 2, ExecutableGuessed: false));
        }

        return new ImportReviewViewModel(list) { ShowExisting = true };
    }

    /// <summary>The dialog in a host the size of the window's dialog area, laid out.</summary>
    private static (Wpf.Ui.Controls.ContentDialog Dialog, Grid Host) Shown(ImportReviewViewModel vm, double height)
    {
        Wpf.Ui.Controls.ContentDialog dialog = ImportReviewPrompt.Create(vm);
        var host = new Grid { Width = 1000, Height = height, Children = { dialog } };
        Visuals.Layout(host, 1000, height);
        return (dialog, host);
    }

    /// <summary>
    /// The text of every text cell against what is painted under it, composited down to the window (the tick box's glyph is an icon, not text). A row of a game already in the
    /// library is drawn at 0.55 opacity on purpose (inactive: its box cannot be ticked) and is measured as drawn before that dimming.
    /// </summary>
    [Theory]
    [InlineData(ApplicationTheme.Dark)]
    [InlineData(ApplicationTheme.Light)]
    public async Task EveryImportRowIsReadable(ApplicationTheme theme)
    {
        List<(string Text, double Ratio)> cells = await PagesLoadTests.OnStaAsync(() => Visuals.UnderTheme(theme, () =>
        {
            Color window = Wcag.Resolve(new ThemesDictionary { Theme = theme }, "ApplicationBackgroundColor");
            (Wpf.Ui.Controls.ContentDialog dialog, Grid host) = Shown(Candidates(4), 760);
            DataGrid grid = Visuals.Find<DataGrid>(dialog)!;
            var measured = new List<(string, double)>();
            foreach (DataGridCell cell in Visuals.FindAll<DataGridCell>(grid).Where(static c => c.Column is DataGridTextColumn))
            {
                foreach (TextBlock text in Visuals.FindAll<TextBlock>(cell).Where(static t => t.Text.Length > 0))
                {
                    Color under = Visuals.PaintedUnder(text, host, window);
                    measured.Add((text.Text, Wcag.Ratio(Visuals.TextOver(text, under), under)));
                }
            }

            return measured;
        }));

        cells.Should().NotBeEmpty("the rows were rendered and their cells hold text");
        cells.Should().OnlyContain(static c => c.Ratio >= Wcag.AA, $"08_UI §Contrast: every row reads at WCAG AA in the {theme} theme (the beta.11 rows were white on white in Dark)");
    }

    /// <summary>
    /// The owner's screenshot showed each tick box as a sliver at the row's left edge: WPF UI's CheckBox style (MinWidth 120,
    /// padding 11) in a 40 px column. Counted in the rendered pixels: a ticked box is the accent's 20×20 square, about 380 accent-blue pixels when
    /// it is drawn whole and about 140 when it is the sliver.
    /// </summary>
    [Fact]
    public async Task TheTickBoxesAreDrawnWhole()
    {
        List<int> accentPixels = await PagesLoadTests.OnStaAsync(() =>
        {
            (Wpf.Ui.Controls.ContentDialog dialog, Grid host) = Shown(Candidates(4), 760);
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1000, 760, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(host);
            byte[] pixels = new byte[1000 * 760 * 4];
            bitmap.CopyPixels(pixels, 1000 * 4, 0);
            var counts = new List<int>();
            foreach (DataGridCell cell in Visuals.FindAll<DataGridCell>(Visuals.Find<DataGrid>(dialog)!))
            {
                if (Visuals.Find<CheckBox>(cell) is { IsChecked: true } box)
                {
                    // Half a row of slack above and below: the dialog's opening transform moves what is drawn a few pixels
                    // from where layout puts it, and the rows on either side hold no ticked box.
                    Rect area = box.TransformToAncestor(host).TransformBounds(new Rect(box.RenderSize));
                    area.Inflate(0, 16);
                    counts.Add(AccentPixels(pixels, 1000, area));
                }
            }

            return counts;
        });

        accentPixels.Should().HaveCount(2, "rows 0 and 3 start ticked; row 1 has no executable and row 2 is already in the library");
        accentPixels.Should().OnlyContain(static n => n >= 250, "a ticked box is drawn whole (about 380 accent pixels), not as a sliver (about 140)");
    }

    /// <summary>Pixels in <paramref name="area"/> of a Pbgra32 bitmap that read as the accent's blue.</summary>
    private static int AccentPixels(byte[] pixels, int width, Rect area)
    {
        int count = 0;
        for (int y = (int)Math.Max(0, area.Top); y < (int)area.Bottom; y++)
        {
            for (int x = (int)Math.Max(0, area.Left); x < (int)Math.Min(width, area.Right); x++)
            {
                int i = ((y * width) + x) * 4;
                byte blue = pixels[i];
                byte red = pixels[i + 2];
                if (blue > 150 && red < 110)
                {
                    count++;
                }
            }
        }

        return count;
    }

    [Fact]
    public async Task ALongListScrollsInsideTheListAndTheIntroStaysVisible()
    {
        (double DialogScrollable, double ListScrollable, double FooterBottom, double Viewport) layout = await PagesLoadTests.OnStaAsync(() =>
        {
            (Wpf.Ui.Controls.ContentDialog dialog, _) = Shown(Candidates(60), 600);
            var body = (ScrollViewer)dialog.Template.FindName("PART_ContentScroll", dialog);
            var content = (FrameworkElement)body.Content;
            DataGrid grid = Visuals.Find<DataGrid>(content)!;
            ScrollViewer list = Visuals.Find<ScrollViewer>(grid)!;
            FrameworkElement footer = Visuals.FindAll<Wpf.Ui.Controls.TextBlock>(content).Last();
            double footerBottom = footer.TransformToAncestor(body).Transform(new Point(0, footer.ActualHeight)).Y;
            return (body.ScrollableHeight, list.ScrollableHeight, footerBottom, body.ViewportHeight);
        });

        layout.DialogScrollable.Should().Be(0, "the dialog's own body does not scroll: nothing above the list can be carried away");
        layout.ListScrollable.Should().BeGreaterThan(0, "sixty rows scroll inside the list");
        layout.FooterBottom.Should().BeLessThanOrEqualTo(layout.Viewport + 0.5, "the selection count under the list is inside the dialog, not cut off");
    }
}
