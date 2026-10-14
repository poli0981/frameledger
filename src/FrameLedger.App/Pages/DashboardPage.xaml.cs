// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.App.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace FrameLedger.App.Pages;

public partial class DashboardPage : INavigableView<DashboardViewModel>
{
    public DashboardPage(DashboardViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = this;
        InitializeComponent();

        // beta.23: the view model hears the Agent while this page is on screen, never after (each visit makes a new one).
        Loaded += (_, _) => ViewModel.Attach();
        Unloaded += (_, _) => ViewModel.Detach();
    }

    public DashboardViewModel ViewModel { get; }
}
