// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Controls;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Dialogs;

/// <summary>The body of Tools ▸ Database maintenance's <c>ContentDialog</c> (P4 PR-7).</summary>
public partial class DatabaseMaintenanceContent : UserControl
{
    public DatabaseMaintenanceContent(DatabaseMaintenanceViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        InitializeComponent();
    }

    public DatabaseMaintenanceViewModel ViewModel { get; }
}
