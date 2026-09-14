using System.Windows.Data;
using FrameLedger.App.Dialogs;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Import;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>FR-1.2's review checklist in WPF UI: a <c>ContentDialog</c> whose primary button follows the tick count; Cancel adds nothing.</summary>
public sealed class ImportReviewPrompt : IImportReview
{
    private readonly IContentDialogService _dialogs;

    public ImportReviewPrompt(IContentDialogService dialogs) => _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    public async Task<IReadOnlyList<ImportCandidate>?> ReviewAsync(IReadOnlyList<ImportCandidate> candidates, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var viewModel = new ImportReviewViewModel(candidates);
        var dialog = new ContentDialog
        {
            Title = ImportReviewViewModel.Title,
            Content = new ImportReviewContent(viewModel),
            PrimaryButtonText = Strings.Import_Add,
            CloseButtonText = Strings.Common_Cancel,
            DefaultButton = ContentDialogButton.Primary,
            DialogMaxWidth = 900,
        };
        _ = dialog.SetBinding(ContentDialog.IsPrimaryButtonEnabledProperty, new Binding(nameof(ImportReviewViewModel.CanImport)) { Source = viewModel, Mode = BindingMode.OneWay });

        ContentDialogResult result = await _dialogs.ShowAsync(dialog, ct).ConfigureAwait(true);
        return result == ContentDialogResult.Primary ? viewModel.Selected : null;
    }
}
