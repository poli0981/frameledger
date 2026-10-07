// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using FluentAssertions;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>
/// <c>ui.hide_scrollbars</c> (beta.14, owner request 2026-10-05: "an option to hide the scroll bars but still scroll"). Each
/// check builds the scrollers the App shows — a plain one, a DataGrid's, a multi-line TextBox's, a rendered document's, and
/// one whose axis is Disabled — and turns the option on and off within one job on the STA thread. The option is process-wide
/// state, so the class runs with the other classes that write such state, one at a time.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class ScrollBarHidingTests
{
    /// <summary>Every kind of scroller the App has, each with more content than its height, laid out in a column.</summary>
    private static (StackPanel Root, ScrollViewer Plain, ScrollViewer Disabled) Scrollers()
    {
        var plain = new ScrollViewer { Height = 120, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new Border { Height = 600 } };
        var disabled = new ScrollViewer { Height = 120, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = new TextBlock { Text = "wraps" } };
        var grid = new DataGrid { Height = 120, ItemsSource = Enumerable.Range(1, 50).Select(static i => new { Row = i }).ToList() };
        var text = new TextBox
        {
            Height = 120,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = string.Join(Environment.NewLine, Enumerable.Range(1, 60).Select(static i => "line " + i.ToString(System.Globalization.CultureInfo.InvariantCulture))),
        };
        var document = new FlowDocumentScrollViewer { Height = 120, Document = new FlowDocument(new Paragraph(new Run(string.Join(" ", Enumerable.Repeat("word", 600))))) };
        var root = new StackPanel { Width = 600, Children = { plain, disabled, grid, text, document } };
        Visuals.Layout(root, 600, 900);
        return (root, plain, disabled);
    }

    private static List<ScrollViewer> All(DependencyObject root) => [.. Visuals.FindAll<ScrollViewer>(root)];

    [Fact]
    public async Task OnHidesEveryBarAndTheWheelStillScrolls()
    {
        (int Viewers, List<string> Shown, ScrollBarVisibility DisabledAxis, double Offset) result = await PagesLoadTests.OnStaAsync(() =>
        {
            (StackPanel root, ScrollViewer plain, ScrollViewer disabled) = Scrollers();
            try
            {
                ScrollBarHiding.Apply(true);
                ScrollBarHiding.ApplyUnder(root);
                root.UpdateLayout();
                List<ScrollViewer> viewers = All(root);
                List<string> shown = [.. viewers.Where(static v => v.ComputedVerticalScrollBarVisibility == Visibility.Visible || v.ComputedHorizontalScrollBarVisibility == Visibility.Visible)
                    .Select(static v => (v.TemplatedParent ?? v).GetType().Name)];

                var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = UIElement.MouseWheelEvent };
                ((UIElement)plain.Content).RaiseEvent(wheel);
                root.UpdateLayout();
                return (viewers.Count, shown, disabled.VerticalScrollBarVisibility, plain.VerticalOffset);
            }
            finally
            {
                ScrollBarHiding.Apply(false);
            }
        });

        result.Viewers.Should().BeGreaterThanOrEqualTo(5, "the plain scroller, the disabled one and those inside the grid, the box and the document");
        result.Shown.Should().BeEmpty("no scroller draws a bar while the option is on");
        result.DisabledAxis.Should().Be(ScrollBarVisibility.Disabled, "a disabled axis lays its content out at the viewport's size, and is left alone");
        result.Offset.Should().BeGreaterThan(0, "a hidden bar is still a scroller: the wheel moves it");
    }

    [Fact]
    public async Task OffRestoresWhatEachViewerHad()
    {
        (List<ScrollBarVisibility> Before, List<ScrollBarVisibility> Hidden, List<ScrollBarVisibility> After) result = await PagesLoadTests.OnStaAsync(() =>
        {
            (StackPanel root, _, _) = Scrollers();
            List<ScrollBarVisibility> Read() => [.. All(root).Select(static v => v.VerticalScrollBarVisibility)];
            List<ScrollBarVisibility> before = Read();
            try
            {
                ScrollBarHiding.Apply(true);
                ScrollBarHiding.ApplyUnder(root);
                List<ScrollBarVisibility> hidden = Read();
                ScrollBarHiding.Apply(false);
                ScrollBarHiding.ApplyUnder(root);
                return (before, hidden, Read());
            }
            finally
            {
                ScrollBarHiding.Apply(false);
            }
        });

        result.Hidden.Should().NotEqual(result.Before, "the option changed something");
        result.After.Should().Equal(result.Before, "off puts back every scroller's own setting");
    }

    [Fact]
    public async Task AScrollerThatLoadsWhileOnIsHiddenAndOneThatLoadsAfterOffIsLeftAlone()
    {
        (ScrollBarVisibility WhileOn, ScrollBarVisibility FirstAfterOff, ScrollBarVisibility LoadedAfterOff) result = await PagesLoadTests.OnStaAsync(() =>
        {
            ScrollBarHiding.Register();
            try
            {
                ScrollBarHiding.Apply(true);
                var first = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                first.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, first));
                ScrollBarVisibility whileOn = first.VerticalScrollBarVisibility;

                ScrollBarHiding.Apply(false);
                first.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, first));
                var second = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Visible };
                second.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, second));
                return (whileOn, first.VerticalScrollBarVisibility, second.VerticalScrollBarVisibility);
            }
            finally
            {
                ScrollBarHiding.Apply(false);
            }
        });

        result.WhileOn.Should().Be(ScrollBarVisibility.Hidden, "the class handler reaches a scroller as it loads");
        result.FirstAfterOff.Should().Be(ScrollBarVisibility.Auto, "and gives it back its own setting on its next load after off");
        result.LoadedAfterOff.Should().Be(ScrollBarVisibility.Visible, "a scroller the option never touched keeps what it has");
    }

    /// <summary>
    /// beta.17 (the owner's screenshot of 2026-10-07: the Dashboard kept its bar with the option on). WPF raises Loaded only on
    /// an element whose subtree listens for it with a handler of its own — an instance handler, a style's EventSetter or a
    /// template's trigger (<c>BroadcastEventHelper</c>, <c>FrameworkElement.ThisHasLoadedChangeEventHandler</c>) — and a
    /// class handler is none of those, so a scroller over plain cards and text never loaded as far as the option knew. Laid
    /// out with no Loaded at all, a scroller must be hidden all the same.
    /// </summary>
    [Fact]
    public async Task AScrollerLaidOutWithNoLoadedEventIsHidden()
    {
        (ScrollBarVisibility Axis, Visibility Bar, double Scrollable) result = await PagesLoadTests.OnStaAsync(() =>
        {
            ScrollBarHiding.Register();
            try
            {
                ScrollBarHiding.Apply(true);
                var viewer = new ScrollViewer { Height = 120, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new Border { Height = 600 } };
                Visuals.Layout(viewer, 300, 120);
                return (viewer.VerticalScrollBarVisibility, viewer.ComputedVerticalScrollBarVisibility, viewer.ScrollableHeight);
            }
            finally
            {
                ScrollBarHiding.Apply(false);
            }
        });

        result.Scrollable.Should().BeGreaterThan(0, "the scroller has something to scroll");
        result.Axis.Should().Be(ScrollBarVisibility.Hidden, "the option reaches a scroller when it is laid out, Loaded or not");
        result.Bar.Should().NotBe(Visibility.Visible);
    }

    /// <summary>
    /// The option as the owner has it: on at start-up (<c>App.RunAsync</c> applies it before the first window), then every
    /// surface shown in a real window off the screen — each page navigated in WPF UI's presenter as the shell does, the
    /// secondary windows, the first run, every dialog's content. No scroller that has something to scroll draws a bar. Before
    /// beta.17 Settings passed (its toggle switches' template has a Loaded trigger, so its scroller loaded) and the Dashboard
    /// did not; Settings and the Dashboard must both be scrollable here, or the check proves nothing.
    /// </summary>
    [Fact]
    public async Task EverySurfaceShownWithTheOptionOnDrawsNoBar()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        using PagesLoadTests.Surfaces surfaces = await PagesLoadTests.Surfaces.LoadAsync(s);

        (List<string> Bars, HashSet<string> Scrollable) result = await PagesLoadTests.OnStaAsync(() =>
        {
            ScrollBarHiding.Register();
            ScrollBarHiding.Apply(true);
            try
            {
                var bars = new List<string>();
                var scrollable = new HashSet<string>(StringComparer.Ordinal);
                foreach ((string name, Func<FrameworkElement> build) in surfaces.Pages())
                {
                    Sweep(name, () => InPresenter(build()), bars, scrollable);
                }

                foreach ((string name, Func<FrameworkElement> build) in surfaces.Windows().Concat(surfaces.Dialogs()))
                {
                    Sweep(name, build, bars, scrollable);
                }

                return (bars, scrollable);
            }
            finally
            {
                ScrollBarHiding.Apply(false);
            }
        });

        result.Scrollable.Should().Contain(["Dashboard", "Settings", "Logs", "User guide"], "the check is vacuous unless those scroll in a window this small");
        string.Join(Environment.NewLine, result.Bars).Should().BeEmpty("ui.hide_scrollbars: no scroller the App shows draws a bar, and the wheel still scrolls it");
    }

    /// <summary>
    /// The harness against WPF itself: a scroller of our own with a Loaded class handler, shown in a real window, is never
    /// called while it holds only a Border, and is called once it holds a CheckBox, whose template listens for Loaded. If the
    /// first half fails, WPF raises Loaded everywhere and the reason above is gone; if the second fails, the harness does not
    /// deliver Loaded and <see cref="EverySurfaceShownWithTheOptionOnDrawsNoBar"/> could not tell Settings from the Dashboard.
    /// </summary>
    [Fact]
    public async Task WpfRaisesLoadedOnlyWhereASubtreeListensForIt()
    {
        (int WithBorder, int WithCheckBox) loads = await PagesLoadTests.OnStaAsync(() =>
            (LoadsOfAProbeHolding(new Border { Height = 40 }), LoadsOfAProbeHolding(new CheckBox { Content = "listens" })));

        loads.WithBorder.Should().Be(0, "a class handler does not put an element on WPF's Loaded route");
        loads.WithCheckBox.Should().BeGreaterThan(0, "WPF UI's CheckBox template has a Loaded trigger, which does");
    }

    private sealed class ProbeScrollViewer : ScrollViewer
    {
        static ProbeScrollViewer() => EventManager.RegisterClassHandler(typeof(ProbeScrollViewer), LoadedEvent, new RoutedEventHandler(static (_, _) => Loads++), handledEventsToo: true);

        public static int Loads { get; private set; }
    }

    private static int LoadsOfAProbeHolding(UIElement content)
    {
        var probe = new ProbeScrollViewer { Content = content };
        int before = ProbeScrollViewer.Loads;
        Window window = OffScreen(new Window { Content = probe });
        try
        {
            window.Show();
            Visuals.Pump();
            return ProbeScrollViewer.Loads - before;
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A page navigated in WPF UI's presenter, as the shell's NavigationView hosts it.</summary>
    private static Grid InPresenter(FrameworkElement page)
    {
        var presenter = new Wpf.Ui.Controls.NavigationViewContentPresenter();
        _ = presenter.Navigate(page);
        return new Grid { Children = { presenter } };
    }

    /// <summary>
    /// Shows the surface in a real window off the screen, lets WPF load and lay it out, and notes every scroller that has
    /// something to scroll and every one of those that draws a bar.
    /// </summary>
    private static void Sweep(string name, Func<FrameworkElement> build, List<string> bars, HashSet<string> scrollable)
    {
        FrameworkElement built = build();
        Window window = OffScreen(built as Window ?? new Window { Content = built });
        try
        {
            window.Show();
            Visuals.Pump();
            if (Visuals.Find<LogsPage>(window) is { } logs)
            {
                // The tail is empty in a scratch folder; fill it as a log file would.
                logs.Tail.Text = string.Join(Environment.NewLine, Enumerable.Range(1, 200).Select(static i => "line " + i.ToString(CultureInfo.InvariantCulture)));
                Visuals.Pump();
            }

            foreach (ScrollViewer viewer in Visuals.FindAll<ScrollViewer>(window))
            {
                bool vertical = viewer.ScrollableHeight > 0.5;
                bool horizontal = viewer.ScrollableWidth > 0.5;
                if (vertical || horizontal)
                {
                    _ = scrollable.Add(name);
                }

                if ((vertical && viewer.ComputedVerticalScrollBarVisibility == Visibility.Visible)
                    || (horizontal && viewer.ComputedHorizontalScrollBarVisibility == Visibility.Visible))
                {
                    bars.Add(string.Create(CultureInfo.InvariantCulture,
                        $"{name}: {(viewer.TemplatedParent ?? viewer).GetType().Name} draws a bar ({viewer.ScrollableWidth:0} x {viewer.ScrollableHeight:0} to scroll)"));
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A window shown where nobody sees it and never activated, small enough that the surfaces overflow it.</summary>
    private static Window OffScreen(Window window)
    {
        window.Width = 1000;
        window.Height = 420;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -20000;
        window.Top = -20000;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        return window;
    }
}
