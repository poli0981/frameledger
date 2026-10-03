// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Controls;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Dialogs;

/// <summary>The body of the bug report's optional-items <c>ContentDialog</c> (P4 PR-9): the crash dump's checkbox.</summary>
public partial class BugBundleOptionsContent : UserControl
{
    public BugBundleOptionsContent(BugBundleOptionsViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        InitializeComponent();
    }

    public BugBundleOptionsViewModel ViewModel { get; }
}
