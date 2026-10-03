// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Controls;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Dialogs;

/// <summary>The body of FR-1.2's review <c>ContentDialog</c> (P4 PR-4).</summary>
public partial class ImportReviewContent : UserControl
{
    public ImportReviewContent(ImportReviewViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        InitializeComponent();
    }

    public ImportReviewViewModel ViewModel { get; }
}
