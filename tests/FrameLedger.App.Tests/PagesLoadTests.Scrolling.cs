// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FluentAssertions;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Infrastructure.Persistence;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Tests;

/// <summary>
/// The mouse wheel on the pages (beta.14, owner: "the wheel does not scroll, I have to drag the scroll bar"). WPF UI 4.3.0's
/// <see cref="NavigationViewContentPresenter"/> wraps a page in a <c>DynamicScrollViewer</c> unless the page sets
/// <c>ScrollViewer.CanContentScroll="False"</c>; inside it the page's own root scroller had infinite height, nothing to
/// scroll, and took every wheel all the same. Each check hosts a page the way the App does — navigated in the presenter.
/// </summary>
public sealed partial class PagesLoadTests
{
    /// <summary>A page navigated in WPF UI's presenter and laid out in an area the size of the window's.</summary>
    private static NavigationViewContentPresenter Navigated(System.Windows.Controls.Page page, double height)
    {
        var presenter = new NavigationViewContentPresenter();
        var area = new Grid { Width = 1000, Height = height, Children = { presenter } };
        Visuals.Layout(area, 1000, height);
        _ = presenter.Navigate(page);
        Visuals.Pump();
        Visuals.Layout(area, 1000, height);
        Visuals.Pump();
        area.UpdateLayout();
        return presenter;
    }

    private static void Wheel(UIElement over, int delta)
    {
        var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
        over.RaiseEvent(wheel);
        wheel.RoutedEvent = UIElement.MouseWheelEvent;
        over.RaiseEvent(wheel);
    }

    [Fact]
    public async Task ANavigatedPageIsNotWrappedInTheDynamicScrollViewerAndTheWheelScrollsIt()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var consent = new HookingConsent(new NoAgent(), new NoPrompt());
        (SettingsViewModel settings, LogsViewModel logs) = await SettingsAndLogsAsync(s, consent, new NoStrip());
        using LogsViewModel ownedLogs = logs;

        (bool Wrapped, double Offset) result = await OnStaAsync(() =>
        {
            var page = new SettingsPage(settings, new SystemInfoViewModel(new FixedHardware(), new NoClipboard(), new NoStrip()));
            NavigationViewContentPresenter presenter = Navigated(page, 500);
            System.Windows.Controls.ScrollViewer root = Visuals.Find<System.Windows.Controls.ScrollViewer>(page)!;
            Wheel(Visuals.Leaf((UIElement)root.Content), -120);
            presenter.UpdateLayout();
            return (presenter.IsDynamicScrollViewerEnabled, root.VerticalOffset);
        });

        result.Wrapped.Should().BeFalse("the page opts out of WPF UI's wrapper (ScrollViewer.CanContentScroll=\"False\")");
        result.Offset.Should().BeGreaterThan(0, "the wheel over the page's text scrolls the page");
    }

    [Fact]
    public async Task TheLogsTailIsBoundedAndFollowsTheEnd()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        var consent = new HookingConsent(new NoAgent(), new NoPrompt());
        (_, LogsViewModel logs) = await SettingsAndLogsAsync(s, consent, new NoStrip());
        using LogsViewModel ownedLogs = logs;

        (double Height, double Offset) tail = await OnStaAsync(() =>
        {
            var page = new LogsPage(logs);
            _ = Navigated(page, 500);
            page.Tail.Text = string.Join(Environment.NewLine, Enumerable.Range(1, 400).Select(static i => "line " + i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            page.UpdateLayout();
            page.Tail.ScrollToEnd();
            page.UpdateLayout();
            return (page.Tail.ActualHeight, page.Tail.VerticalOffset);
        });

        tail.Height.Should().BeLessThan(500, "the tail has the height the page leaves it, not one tall enough for every line");
        tail.Offset.Should().BeGreaterThan(0, "so it scrolls, and ScrollToEnd (autoscroll) shows the newest line");
    }
}
