// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.App.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace FrameLedger.App.Pages;

public partial class SettingsPage : INavigableView<SettingsViewModel>
{
    public SettingsPage(SettingsViewModel viewModel, SystemInfoViewModel system)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        SystemInfo = system ?? throw new ArgumentNullException(nameof(system));
        DataContext = this;
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }

    /// <summary>Settings ▸ System: this PC as a session's hardware snapshot would record it (2026-09-21).</summary>
    public SystemInfoViewModel SystemInfo { get; }
}
