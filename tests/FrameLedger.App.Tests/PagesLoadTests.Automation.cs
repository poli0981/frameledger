// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FluentAssertions;

namespace FrameLedger.App.Tests;

/// <summary>
/// What a screen reader is given (beta.35, D88): the UI Automation tree WPF builds for every surface, hosted as the App
/// hosts it, with every tab of a TabControl shown in turn. These are measurements of the tree, not of speech: what Narrator
/// says is read from this tree, so a name missing here is a name it cannot say. The owner's Narrator pass of 2026-11-09
/// found no fault and could not see these — a record's printed form read as an item's name, a Tab stop with nothing to
/// say, a notice's text hidden inside a template.
/// </summary>
public sealed partial class PagesLoadTests
{
    /// <summary>A record's printed form — <c>Choice { Value = dark, Label = Dark }</c> — which WPF reads as an item's name.</summary>
    [GeneratedRegex(@"^\w+(`\d+)? \{ .*\}$", RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex RecordDump();

    /// <summary>Every surface, every tab shown in turn: the surface's name and its root, laid out, for each visit.</summary>
    private static List<string> SweepEverySurface(Surfaces surfaces, Action<string, FrameworkElement, List<string>> check)
    {
        var found = new List<string>();
        foreach ((string name, Func<FrameworkElement> build) in surfaces.All())
        {
            (Window host, FrameworkElement root) = Hosted(build);
            try
            {
                Lay(root);
                check(name, root, found);
                foreach (TabControl tabs in Visuals.FindAll<TabControl>(root).ToList())
                {
                    for (int i = 0; i < tabs.Items.Count; i++)
                    {
                        tabs.SelectedIndex = i;
                        Visuals.Pump();
                        Lay(root);
                        check(string.Create(CultureInfo.InvariantCulture, $"{name} (tab {i})"), root, found);
                    }
                }
            }
            finally
            {
                host.Close();
            }
        }

        return [.. found.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>The elements a user can reach: nothing above them collapsed or hidden, none disabled.</summary>
    private static IEnumerable<UIElement> Reachable(DependencyObject root)
    {
        if (root is UIElement { Visibility: not Visibility.Visible } or UIElement { IsEnabled: false })
        {
            yield break;
        }

        if (root is UIElement element)
        {
            yield return element;
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            foreach (UIElement child in Reachable(VisualTreeHelper.GetChild(root, i)))
            {
                yield return child;
            }
        }
    }

    private static bool IsTabStop(UIElement element) => element.Focusable && KeyboardNavigation.GetIsTabStop(element);

    private static string Describe(string surface, DependencyObject element)
    {
        string name = element is FrameworkElement { Name.Length: > 0 } named ? "#" + named.Name : string.Empty;
        string context = element is FrameworkElement { DataContext: { } data } ? " over " + data.GetType().Name : string.Empty;
        return $"{surface}: {element.GetType().Name}{name}{context}";
    }

    /// <summary>
    /// A control that takes the focus but has no automation peer is a Tab stop Narrator says nothing about: WPF raises a
    /// focus event only for an element with a peer. Plain <c>Control</c>s, WPF UI's <c>Image</c> and ScottPlot's plot are such
    /// controls unless they are given a peer or taken out of the tab order.
    /// </summary>
    [Fact]
    public async Task NoSurfaceHasASilentTabStop()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        using Surfaces surfaces = await Surfaces.LoadAsync(s);

        List<string> silent = await OnStaAsync(() => SweepEverySurface(surfaces, static (name, root, found) =>
        {
            foreach (UIElement element in Reachable(root).Where(IsTabStop))
            {
                if (UIElementAutomationPeer.CreatePeerForElement(element) is null && !string.Equals(element.GetType().Name, "WpfPlot", StringComparison.Ordinal))
                {
                    found.Add(Describe(name, element));
                }
            }
        }));

        string.Join(Environment.NewLine, silent).Should().BeEmpty("a Tab stop with no automation peer says nothing to a screen reader (ScottPlot's plot is beta.35 PR-4's)");
    }

    /// <summary>
    /// WPF names a list's item by the item's <c>ToString()</c> — a drop-down's options, a list's rows, a grid's cells with no
    /// text of their own — so an item type that does not say what it shows is read as its type, or a record's whole printed
    /// form. Measured 2026-11-09: a theme option was <c>Choice { Value = dark, Label = Dark }</c>.
    /// </summary>
    [Fact]
    public async Task NoSurfaceNamesAnItemByItsType()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        using Surfaces surfaces = await Surfaces.LoadAsync(s);

        List<string> typeNamed = await OnStaAsync(() => SweepEverySurface(surfaces, static (name, root, found) =>
        {
            foreach (ItemsControl list in Reachable(root).OfType<ItemsControl>())
            {
                foreach (object? item in list.Items)
                {
                    if (item is null or string or ValueType and IConvertible || item is UIElement)
                    {
                        continue;
                    }

                    string text = item.ToString() ?? string.Empty;
                    if (text.Length == 0 || string.Equals(text, item.GetType().ToString(), StringComparison.Ordinal) || RecordDump().IsMatch(text))
                    {
                        found.Add($"{name}: {list.GetType().Name}{(list.Name.Length > 0 ? "#" + list.Name : string.Empty)} item {item.GetType().Name} reads \"{Shorten(text)}\"");
                    }
                }
            }

            // A grid's cells as the grid's peer hands them out: a cell with no text of its own falls back to WPF's
            // "Item: …, Column Display Index: n".
            foreach (DataGrid grid in Reachable(root).OfType<DataGrid>())
            {
                foreach (AutomationPeer row in UIElementAutomationPeer.CreatePeerForElement(grid)?.GetChildren() ?? [])
                {
                    foreach (AutomationPeer cell in row.GetChildren() ?? [])
                    {
                        string cellName = cell.GetName();
                        if (cellName.StartsWith("Item: ", StringComparison.Ordinal) || cellName.Contains("FrameLedger.", StringComparison.Ordinal) || RecordDump().IsMatch(cellName))
                        {
                            found.Add($"{name}: {grid.GetType().Name}{(grid.Name.Length > 0 ? "#" + grid.Name : string.Empty)} cell reads \"{Shorten(cellName)}\"");
                        }
                    }
                }
            }
        }));

        string.Join(Environment.NewLine, typeNamed).Should().BeEmpty("an item is read by the text it shows, never by its type");
    }

    /// <summary>Every control the keyboard reaches has a name a screen reader can say.</summary>
    [Fact]
    public async Task EveryFocusableControlOnEverySurfaceHasAName()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        using Surfaces surfaces = await Surfaces.LoadAsync(s);

        List<string> unnamed = await OnStaAsync(() => SweepEverySurface(surfaces, static (name, root, found) =>
        {
            foreach (UIElement element in Reachable(root).Where(IsTabStop))
            {
                if (UIElementAutomationPeer.CreatePeerForElement(element) is { } peer && string.IsNullOrWhiteSpace(peer.GetName()) && !Anonymous(element))
                {
                    found.Add(Describe(name, element) + " (" + peer.GetType().Name + ")");
                }
            }
        }));

        string.Join(Environment.NewLine, unnamed).Should().BeEmpty("a control the keyboard reaches is announced by its name; an unnamed one is read as its type alone");
    }

    /// <summary>
    /// The containers a screen reader passes through without stopping to read them — a scroller, a list's own panel — whose
    /// content carries the names.
    /// </summary>
    private static bool Anonymous(UIElement element) => element is ScrollViewer or ScrollBar or Thumb or Frame;

    /// <summary>
    /// WPF hides a TextBlock inside a control's template from the control view of the UI Automation tree (its
    /// <c>TextBlockAutomationPeer</c> is no control element when its templated parent is not a <c>ContentPresenter</c>). Such a
    /// text reaches a screen reader only through its control: the control has a peer, and the peer has a name. The safety
    /// notices' title and message were such texts in the App's own InfoBar template.
    /// </summary>
    [Fact]
    public async Task NoShownTextIsHiddenFromAScreenReader()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        using Surfaces surfaces = await Surfaces.LoadAsync(s);

        List<string> hidden = await OnStaAsync(() => SweepEverySurface(surfaces, static (name, root, found) =>
        {
            foreach (TextBlock text in ShownTexts(root))
            {
                if (text.TemplatedParent is null or ContentPresenter || text.TemplatedParent is not UIElement owner)
                {
                    continue;
                }

                AutomationPeer? peer = UIElementAutomationPeer.CreatePeerForElement(owner);
                if ((peer is null || string.IsNullOrWhiteSpace(peer.GetName())) && owner is not Wpf.Ui.Controls.TitleBar)
                {
                    found.Add($"{name}: \"{Shorten(text.Text)}\" inside {owner.GetType().Name}'s template ({(peer is null ? "no peer" : "a peer with no name")})");
                }
            }
        }));

        string.Join(Environment.NewLine, hidden).Should().BeEmpty("text inside a template reaches a screen reader only through its control's peer and name");
    }

    /// <summary>
    /// Buttons repeated in a list say which row they act on: three *Open* buttons in a game's sessions, read one after another,
    /// do not say which session each opens.
    /// </summary>
    [Fact]
    public async Task NoTwoButtonsInOneListShareAName()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        using Surfaces surfaces = await Surfaces.LoadAsync(s);

        List<string> twins = await OnStaAsync(() => SweepEverySurface(surfaces, static (name, root, found) =>
        {
            foreach (ItemsControl list in Reachable(root).OfType<ItemsControl>().Where(static l => l.Items.Count > 1 && l is not MenuBase and not ComboBox and not TabControl))
            {
                // One entry per row: the names of the buttons each realised row holds; a name found in two rows is a twin.
                var rowsByName = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < list.Items.Count; i++)
                {
                    if (list.ItemContainerGenerator.ContainerFromIndex(i) is not DependencyObject row)
                    {
                        continue;
                    }

                    foreach (string button in Reachable(row).OfType<ButtonBase>().Where(static b => b is not ToggleButton)
                                 .Select(static b => UIElementAutomationPeer.CreatePeerForElement(b)?.GetName() ?? string.Empty)
                                 .Where(static n => n.Length > 0).Distinct(StringComparer.Ordinal))
                    {
                        rowsByName[button] = rowsByName.GetValueOrDefault(button) + 1;
                    }
                }

                foreach ((string button, int rows) in rowsByName.Where(static p => p.Value > 1))
                {
                    found.Add($"{name}: {rows} rows each hold a button named \"{button}\" in {list.GetType().Name}{(list.Name.Length > 0 ? "#" + list.Name : string.Empty)}");
                }
            }
        }));

        string.Join(Environment.NewLine, twins).Should().BeEmpty("a button repeated on each row names its row");
    }

    private static string Shorten(string text) => text.Length <= 80 ? text : text[..77] + "...";
}
