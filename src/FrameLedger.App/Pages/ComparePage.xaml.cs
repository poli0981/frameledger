// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows;
using FrameLedger.App.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace FrameLedger.App.Pages;

/// <summary>Compare; the chart redraws whenever the view model rebuilt its curves.</summary>
public partial class ComparePage : INavigableView<CompareViewModel>
{
    public ComparePage(CompareViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = this;
        InitializeComponent();
        ViewModel.Compared2 += OnCompared;
        Unloaded += (_, _) => ViewModel.Compared2 -= OnCompared;
    }

    public CompareViewModel ViewModel { get; }

    private void OnCompared(object? sender, EventArgs e) => Chart.Show(ViewModel.Curves, ViewModel.TierLegend);

    private void OnExportPng(object sender, RoutedEventArgs e) => _ = ViewModel.ExportPngAsync(Chart.ScottPlot);
}
