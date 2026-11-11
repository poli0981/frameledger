// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Xml.Linq;
using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Tests;

/// <summary>The Advanced page in a real window (owner decisions D101 and D105): its navigation item, its tables, its charts.</summary>
public sealed partial class PagesLoadTests
{
    /// <summary>
    /// D105: the item is in the pane only while the option is on. The shell's XAML binds its <c>Visibility</c> to
    /// <see cref="AdvancedOption.IsOn"/>; this holds that binding in place and proves the control honours it — a
    /// <c>NavigationViewItem</c> that ignored <c>Visibility</c> would leave a dead item in the pane with the option off.
    /// </summary>
    [Fact]
    public async Task TheAdvancedNavigationItemTakesNoPlaceWhileTheOptionIsOff()
    {
        XDocument shell = XDocument.Load(System.IO.Path.Combine(AccessibilityTests.RepoRoot(), "src", "FrameLedger.App", "MainWindow.xaml"));
        XElement declared = shell.Descendants().Single(static e => string.Equals(e.Name.LocalName, "NavigationViewItem", StringComparison.Ordinal)
            && (e.Attribute("TargetPageType")?.Value.Contains("AdvancedPage", StringComparison.Ordinal) ?? false));
        declared.Attribute("Visibility")?.Value.Should().Be("{Binding Path=(services:AdvancedOption.IsOn), Converter={StaticResource BoolToVis}}");
        declared.Parent!.Name.LocalName.Should().Be("NavigationView.MenuItems", "the last of the menu's items, above the footer's Settings");
        declared.ElementsAfterSelf().Should().BeEmpty();

        (double Off, double On, double Neighbour) heights = await OnStaAsync(() =>
        {
            bool was = AdvancedOption.IsOn;
            var logs = new NavigationViewItem { Content = "Logs", TargetPageType = typeof(LogsPage) };
            var advanced = new NavigationViewItem { Content = "Advanced", TargetPageType = typeof(AdvancedPage) };
            _ = advanced.SetBinding(UIElement.VisibilityProperty, new Binding
            {
                Path = new PropertyPath("(0)", typeof(AdvancedOption).GetProperty(nameof(AdvancedOption.IsOn))!),
                Converter = new BooleanToVisibilityConverter(),
            });
            var navigation = new NavigationView { PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent, IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed };
            _ = navigation.MenuItems.Add(logs);
            _ = navigation.MenuItems.Add(advanced);
            var host = new FluentWindow { Content = navigation };
            try
            {
                AdvancedOption.IsOn = false;
                Lay(navigation);
                Visuals.Pump();
                double off = advanced.IsVisible ? advanced.ActualHeight : 0;
                AdvancedOption.IsOn = true;
                Lay(navigation);
                Visuals.Pump();
                return (off, advanced.ActualHeight, logs.ActualHeight);
            }
            finally
            {
                AdvancedOption.IsOn = was;
                host.Close();
            }
        });

        heights.Neighbour.Should().BeGreaterThan(0, "the pane lays its items out in this window");
        heights.Off.Should().Be(0, "off, the item takes no place in the pane");
        heights.On.Should().Be(heights.Neighbour, "on, it is an item like the others - without a restart");
    }

    [Fact]
    public async Task TheAdvancedPageDrawsItsFactsItsRowsAndBothCharts()
    {
        await using ScratchLedger s = await ScratchLedger.OpenAsync();
        using Surfaces surfaces = await Surfaces.LoadAsync(s);

        (int Grids, int Rows, int LiveLines, int SessionLines, int Expanders) drawn = await OnStaAsync(() =>
        {
            (Window host, FrameworkElement root) = Hosted(() => new AdvancedPage(surfaces.Advanced));
            try
            {
                Lay(root);
                Visuals.Pump();
                var charts = Visuals.FindAll<AdvancedSeriesChart>(root).ToList();
                AdvancedPage page = Visuals.Find<AdvancedPage>(root) ?? throw new InvalidOperationException("the page is not in the frame");
                int rows = Visuals.FindAll<DataGridRow>(root).Count();
                int expanders = Visuals.FindAll<Expander>(root).Count(static e => e.Visibility == Visibility.Visible);

                // The second tab's content exists once it is selected: the session's chart is drawn from the stored series.
                page.ViewModel.SelectedTab = 1;
                Lay(root);
                Visuals.Pump();
                var session = Visuals.FindAll<AdvancedSeriesChart>(root).ToList();
                return (Visuals.FindAll<System.Windows.Controls.DataGrid>(root).Count(), rows, charts.Sum(static c => c.DrawnLines), session.Sum(static c => c.DrawnLines), expanders);
            }
            finally
            {
                host.Close();
            }
        });

        drawn.Rows.Should().Be(3, "This PC lists the three series the fixture's view holds");
        drawn.Expanders.Should().Be(1, "the driver's global profile is a table, and unfolds");
        drawn.LiveLines.Should().Be(0, "the fixture's feed has no history: nothing was asked, so the live chart is empty and says so");
        drawn.SessionLines.Should().Be(1, "the session's first entry - the card's memory in use - is one line");
        drawn.Grids.Should().BeGreaterThan(0);
    }
}
