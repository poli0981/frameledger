// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Controls;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Dialogs;

/// <summary>The body of FR-2.1's <c>ContentDialog</c>; the buttons are the dialog's own (<see cref="Services.ConsentPrompt"/>).</summary>
public partial class ConsentDialogContent : UserControl
{
    public ConsentDialogContent(ConsentDialogViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => Acknowledgement.Focus();
    }

    public ConsentDialogViewModel ViewModel { get; }
}
