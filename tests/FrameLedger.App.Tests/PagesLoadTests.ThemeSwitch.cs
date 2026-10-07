// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentAssertions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;

namespace FrameLedger.App.Tests;

/// <summary>
/// A theme switch while a surface is on screen (beta.17, the owner's screenshots of 2026-10-07: Settings' descriptions light
/// grey on white after Dark → Light, dark grey on dark after the way back). Every text of a surface that lived through the
/// switch must be drawn in the colour the same surface gets when it is made fresh under the new theme. The surface is hosted
/// in a <see cref="Window"/> — never shown — because a window registers itself in <c>Application.Windows</c> and only those
/// trees hear that the application's dictionaries changed, which is how <c>ApplicationThemeManager.Apply</c> switches.
/// </summary>
public sealed partial class PagesLoadTests
{
    private const int _staleShownPerSurface = 6;

    [Theory]
    [InlineData(ApplicationTheme.Dark)]
    [InlineData(ApplicationTheme.Light)]
    public async Task EveryTextFollowsAThemeSwitch(ApplicationTheme createdUnder)
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        using Surfaces surfaces = await Surfaces.LoadAsync(s);

        (int Texts, List<string> Stale) result = await OnStaAsync(() =>
        {
            int texts = 0;
            var stale = new List<string>();
            foreach ((string name, Func<FrameworkElement> build) in surfaces.All())
            {
                (int count, List<string> found) = SwitchedAgainstFresh(name, build, createdUnder);
                texts += count;
                stale.AddRange(found);
            }

            return (texts, stale);
        });

        result.Texts.Should().BeGreaterThan(300, "every surface was rendered and holds text");
        string.Join(Environment.NewLine, result.Stale).Should().BeEmpty($"a text made under {createdUnder} follows the switch (16_WPFUI_SYNTAX §Gotchas: ui:TextBlock Appearance pins the brush it found first)");
    }

    /// <summary>
    /// The library's half of the beta.17 defect, kept visible: WPF UI 4.3.0's <c>ui:TextBlock Appearance</c> stores the brush it
    /// finds when the property is set (<c>TextBlock.OnAppearanceChanged</c> → <c>TryFindResource</c>) and coerces Foreground to
    /// it, so the text keeps the theme it was made under; the App's <c>SecondaryText</c> style follows. When the first half
    /// fails, upstream follows a switch — the styles may stay, they are plain WPF.
    /// </summary>
    [Fact]
    public async Task WpfUisAppearanceStillKeepsTheThemeATextWasMadeUnder()
    {
        (Color Pinned, Color Styled, Color Light) colours = await OnStaAsync(() =>
        {
            var pinned = new Wpf.Ui.Controls.TextBlock { Text = "pinned", Appearance = Wpf.Ui.Controls.TextColor.Secondary };
            var styled = new Wpf.Ui.Controls.TextBlock { Text = "styled", Style = (Style)System.Windows.Application.Current.FindResource("SecondaryText") };
            var host = new Window { Content = new StackPanel { Children = { pinned, styled } } };
            try
            {
                return Visuals.UnderTheme(ApplicationTheme.Light, () =>
                    (Solid(pinned.Foreground), Solid(styled.Foreground), Wcag.Resolve(new ThemesDictionary { Theme = ApplicationTheme.Light }, "TextFillColorSecondary")));
            }
            finally
            {
                host.Close();
            }
        });

        colours.Styled.Should().Be(colours.Light, "the SecondaryText style follows the switch");
        colours.Pinned.Should().NotBe(colours.Light, "WPF UI 4.3.0's Appearance kept the Dark brush; when this fails, upstream fixed it (16_WPFUI_SYNTAX §Gotchas)");
    }

    /// <summary>
    /// One surface made under <paramref name="createdUnder"/> and laid out, the theme switched (Dark → Light inside
    /// <see cref="Visuals.UnderTheme"/>, or Light → Dark as it restores), and its texts compared with a fresh instance's under
    /// the new theme: the number of texts compared and a line for each one drawn in another colour.
    /// </summary>
    private static (int Count, List<string> Stale) SwitchedAgainstFresh(string name, Func<FrameworkElement> build, ApplicationTheme createdUnder)
    {
        if (createdUnder == ApplicationTheme.Dark)
        {
            (Window host, FrameworkElement switched) = Hosted(build);
            try
            {
                Lay(switched);
                return Visuals.UnderTheme(ApplicationTheme.Light, () => CompareWithFresh(name, switched, build));
            }
            finally
            {
                host.Close();
            }
        }

        (Window lightHost, FrameworkElement made) = Visuals.UnderTheme(ApplicationTheme.Light, () =>
        {
            (Window h, FrameworkElement e) = Hosted(build);
            Lay(e);
            return (h, e);
        });
        try
        {
            return CompareWithFresh(name, made, build);
        }
        finally
        {
            lightHost.Close();
        }
    }

    private static (int Count, List<string> Stale) CompareWithFresh(string name, FrameworkElement switched, Func<FrameworkElement> build)
    {
        Lay(switched);
        (Window host, FrameworkElement fresh) = Hosted(build);
        try
        {
            Lay(fresh);
            List<DrawnText> before = Texts(switched);
            List<DrawnText> after = Texts(fresh);
            Dictionary<string, DrawnText> lived = before.ToDictionary(static d => d.Path, StringComparer.Ordinal);
            Dictionary<string, DrawnText> made = after.ToDictionary(static d => d.Path, StringComparer.Ordinal);

            // Texts are reported, never compared: another test class may change Strings.Culture between the two builds. What is
            // compared is what a user can see (beta.18, when pages moved into the Frame): inside a collapsed part a theme switch
            // re-applies a DataGrid's idle scroll bar, whose arrow glyphs then exist in the switched page only, and the select-all
            // corner — collapsed, every grid here shows column headers only — kept the old theme's disabled colour.
            List<string> stale =
            [
                .. before.Where(d => made.TryGetValue(d.Path, out DrawnText f) && (d.Shown || f.Shown) && !string.Equals(d.Colour, f.Colour, StringComparison.Ordinal))
                    .Select(d => $"{name}: \"{d.Text}\" is {d.Colour}, made fresh {made[d.Path].Colour} ({d.Path})"),
                .. before.Where(d => d.Shown && !made.ContainsKey(d.Path)).Select(d => $"{name}: \"{d.Text}\" shows after the switch only ({d.Path})"),
                .. after.Where(d => d.Shown && !lived.ContainsKey(d.Path)).Select(d => $"{name}: \"{d.Text}\" shows in a fresh one only ({d.Path})"),
            ];
            return (before.Count, stale.Count <= _staleShownPerSurface ? stale : [.. stale.Take(_staleShownPerSurface), $"{name}: … and {stale.Count - _staleShownPerSurface} more"]);
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary>
    /// A surface hosted as the App shows it, in a window that is never shown: a window as itself; a page in WPF UI's
    /// <c>NavigationViewContentPresenter</c>, the shell's Frame — which passes no inherited value into the page, the text
    /// colour included (beta.18; until then pages sat straight in the window here, and their black titles inherited the
    /// window's colour in this check only); anything else as the window's content.
    /// </summary>
    private static (Window Host, FrameworkElement Root) Hosted(Func<FrameworkElement> build)
    {
        FrameworkElement built = build();
        if (built is Window window)
        {
            return (window, (FrameworkElement)window.Content);
        }

        if (built is Page page)
        {
            var frame = new Wpf.Ui.Controls.NavigationViewContentPresenter();
            var shell = new Wpf.Ui.Controls.FluentWindow { Content = frame };
            Lay(frame);
            _ = frame.Navigate(page);
            Visuals.Pump();
            return (shell, frame);
        }

        // A FluentWindow, as the shell is: its style gives the inherited text colour by theme resource too.
        var host = new Wpf.Ui.Controls.FluentWindow { Content = built };
        return (host, built);
    }

    private static void Lay(FrameworkElement root) => Visuals.Layout(root, 1200, 900);

    /// <summary>
    /// Every TextBlock under <paramref name="root"/> in visual-tree order: where it is, what it says, the colour it is drawn
    /// in, and whether it shows — nothing above it collapsed or hidden.
    /// </summary>
    private static List<DrawnText> Texts(DependencyObject root)
    {
        var found = new List<DrawnText>();
        Walk(root, root.GetType().Name, shown: true, found);
        return found;
    }

    private static void Walk(DependencyObject node, string path, bool shown, List<DrawnText> found)
    {
        shown &= node is not UIElement { Visibility: not Visibility.Visible };
        if (node is TextBlock text)
        {
            found.Add(new DrawnText(path, text.Text, text.Foreground is SolidColorBrush solid ? solid.Color.ToString(CultureInfo.InvariantCulture) : text.Foreground?.ToString(CultureInfo.InvariantCulture) ?? "null", shown));
        }

        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(node, i);
            Walk(child, string.Create(CultureInfo.InvariantCulture, $"{path}/{child.GetType().Name}[{i}]"), shown, found);
        }
    }

    private readonly record struct DrawnText(string Path, string Text, string Colour, bool Shown);

    private static Color Solid(Brush brush) => brush is SolidColorBrush solid ? solid.Color : throw new InvalidOperationException("a text whose foreground is not a solid colour");
}
