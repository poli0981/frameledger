// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Input;
using FrameLedger.App.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace FrameLedger.App.Pages;

/// <summary>The game page; which game is <c>GameSelection</c>'s, read by the view model.</summary>
public partial class GameDetailPage : INavigableView<GameDetailViewModel>
{
    public GameDetailPage(GameDetailViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = this;
        InitializeComponent();
        ViewModel.SelectionPresented += OnSelectionPresented;
        ViewModel.TrendPresented += OnTrendPresented;
        Overview.SessionClicked += OnOverviewClicked;
        // The view model starts loading before this page exists, and a load that finished first raised its events to
        // nobody: the Trend tab was blank on every first open (beta.8). Draw what it holds now; a later load redraws.
        OnTrendPresented(this, EventArgs.Empty);
        OnSelectionPresented(this, EventArgs.Empty);
        Loaded += (_, _) => ViewModel.Attach();
        Unloaded += (_, _) =>
        {
            ViewModel.SelectionPresented -= OnSelectionPresented;
            ViewModel.TrendPresented -= OnTrendPresented;
            Overview.SessionClicked -= OnOverviewClicked;
            ViewModel.Detach();
        };
    }

    public GameDetailViewModel ViewModel { get; }

    private void OnSelectionPresented(object? sender, EventArgs e)
    {
        Frametime.Show(ViewModel.SelectedSeries, displayed: false, sensors: false);
        Distribution.Show(ViewModel.SelectedSeries);
        Sensors.Show(ViewModel.SelectedSensors);
        Latency.Show(ViewModel.SelectedSeries, ViewModel.SelectedSession?.Row.LatencyAvgUs, ViewModel.SelectedSession?.Row.LatencyP95Us);
    }

    /// <summary>The Trend tab's lines (beta.11: several metrics at once) and the Sessions tab's overview, from one rebuild.</summary>
    private void OnTrendPresented(object? sender, EventArgs e)
    {
        Trend.Show(ViewModel.TrendLines, ViewModel.HardwareChanges);
        Overview.Show(ViewModel.OverviewLines, ViewModel.HardwareChanges);
    }

    /// <summary>beta.11 (D39): a session picked on the overview is selected in the grid below it, and scrolled to.</summary>
    private void OnOverviewClicked(object? sender, Charts.SessionClickedEventArgs e)
    {
        ViewModel.SelectSession(e.SessionId);
        if (ViewModel.SelectedSession is { } session)
        {
            SessionsGrid.ScrollIntoView(session);
        }
    }

    private void OnSessionDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SessionsGrid.SelectedItem is SessionItemViewModel session)
        {
            ViewModel.OpenSessionCommand.Execute(session);
        }
    }
}
