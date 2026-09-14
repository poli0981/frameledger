using System.Windows.Data;
using FrameLedger.App.Dialogs;
using FrameLedger.App.ViewModels;
using FrameLedger.Application.Persistence;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>FR-1.3's edit dialog in WPF UI: a <c>ContentDialog</c> whose primary button follows <see cref="EditGameViewModel.CanSave"/>.</summary>
public sealed class EditGamePrompt : IEditGamePrompt
{
    private readonly IContentDialogService _dialogs;

    public EditGamePrompt(IContentDialogService dialogs) => _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    public async Task<GameMetadata?> EditAsync(GameMetadata current, string? provenanceJson, CancellationToken ct = default)
    {
        var viewModel = new EditGameViewModel(current, provenanceJson);
        var dialog = new ContentDialog
        {
            Title = EditGameViewModel.Title,
            Content = new EditGameContent(viewModel),
            PrimaryButtonText = Strings.EditGame_Save,
            CloseButtonText = Strings.Common_Cancel,
            DefaultButton = ContentDialogButton.Primary,
        };
        _ = dialog.SetBinding(ContentDialog.IsPrimaryButtonEnabledProperty, new Binding(nameof(EditGameViewModel.CanSave)) { Source = viewModel, Mode = BindingMode.OneWay });

        ContentDialogResult result = await _dialogs.ShowAsync(dialog, ct).ConfigureAwait(true);
        return result == ContentDialogResult.Primary && viewModel.CanSave ? viewModel.Result() : null;
    }
}
