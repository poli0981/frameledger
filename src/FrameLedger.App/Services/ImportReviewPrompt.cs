// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Controls;
using System.Windows.Data;
using FrameLedger.App.Dialogs;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Import;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>
/// FR-1.2's review checklist in WPF UI: a <c>ContentDialog</c> whose primary button follows the tick count; Cancel adds nothing.
/// The dialog's own scrolling is off (beta.14): its content row then has the height the window leaves, the list fills it and is
/// the one thing that scrolls — a scrolling list inside a scrolling dialog pushed the intro out of sight at the list's end.
/// </summary>
public sealed class ImportReviewPrompt : IImportReview
{
    private readonly IContentDialogService _dialogs;

    public ImportReviewPrompt(IContentDialogService dialogs) => _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    public async Task<IReadOnlyList<ImportCandidate>?> ReviewAsync(IReadOnlyList<ImportCandidate> candidates, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var viewModel = new ImportReviewViewModel(candidates);
        ContentDialog dialog = Create(viewModel);
        _ = dialog.SetBinding(ContentDialog.IsPrimaryButtonEnabledProperty, new Binding(nameof(ImportReviewViewModel.CanImport)) { Source = viewModel, Mode = BindingMode.OneWay });

        ContentDialogResult result = await _dialogs.ShowAsync(dialog, ct).ConfigureAwait(true);
        return result == ContentDialogResult.Primary ? viewModel.Selected : null;
    }

    /// <summary>The dialog as shown, without a host: a test lays it out in one of its own.</summary>
    internal static ContentDialog Create(ImportReviewViewModel viewModel, ContentDialogHost? host = null)
    {
        ContentDialog dialog = host is null ? new ContentDialog() : new ContentDialog(host);
        dialog.Title = ImportReviewViewModel.Title;
        dialog.Content = new ImportReviewContent(viewModel);
        dialog.PrimaryButtonText = Strings.Import_Add;
        dialog.CloseButtonText = Strings.Common_Cancel;
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.DialogMaxWidth = 900;
        ScrollViewer.SetVerticalScrollBarVisibility(dialog, ScrollBarVisibility.Disabled);
        return dialog;
    }
}
