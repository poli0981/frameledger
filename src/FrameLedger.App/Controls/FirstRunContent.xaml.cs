// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Controls;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Controls;

/// <summary>The first-run steps as one control, so a test renders it without a window.</summary>
public partial class FirstRunContent : UserControl
{
    public FirstRunContent(FirstRunViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = this;
        InitializeComponent();
        DocumentView.UrlOpener = viewModel.Urls;
        DocumentView.RepositoryLinkRequested += OnRepositoryLink;
    }

    public FirstRunViewModel ViewModel { get; }

    /// <summary>A link from one of the documents to another the gate shows opens it here, not on GitHub.</summary>
    private void OnRepositoryLink(object? sender, RepositoryLinkEventArgs e)
    {
        LegalDocument? linked = ViewModel.Documents.FirstOrDefault(d => string.Equals(d.Path, e.Path, StringComparison.OrdinalIgnoreCase));
        if (linked is not null)
        {
            ViewModel.SelectedDocument = linked;
            e.Handled = true;
        }
    }
}
