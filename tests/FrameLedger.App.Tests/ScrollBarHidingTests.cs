// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using FluentAssertions;
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
}
