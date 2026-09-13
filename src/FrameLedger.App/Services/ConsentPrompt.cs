using System.Windows.Data;
using FrameLedger.App.Dialogs;
using FrameLedger.App.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>
/// FR-2.1's dialog in WPF UI (<c>16_WPFUI_SYNTAX</c> §Dialogs: <c>IContentDialogService</c> over the shell's
/// <c>ContentDialogHost</c>). The primary button is bound to the typed acknowledgement and is disabled until the
/// phrase matches; the close button keeps hooking off and is the default, so Enter never enables.
/// </summary>
public sealed class ConsentPrompt : IConsentPrompt
{
    private readonly IContentDialogService _dialogs;

    public ConsentPrompt(IContentDialogService dialogs) => _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    public async Task<bool> ShowAsync(string gameName, CancellationToken ct = default)
    {
        var viewModel = new ConsentDialogViewModel(gameName);
        var dialog = new ContentDialog
        {
            Title = viewModel.Title,
            Content = new ConsentDialogContent(viewModel),
            PrimaryButtonText = ConsentDialogViewModel.EnableText,
            CloseButtonText = ConsentDialogViewModel.KeepOffText,
            // Enter keeps hooking off; only a click on the (acknowledgement-gated) primary enables.
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            DialogMaxWidth = 700,
        };
        _ = dialog.SetBinding(ContentDialog.IsPrimaryButtonEnabledProperty, new Binding(nameof(ConsentDialogViewModel.IsAcknowledged)) { Source = viewModel, Mode = BindingMode.OneWay });

        ContentDialogResult result = await _dialogs.ShowAsync(dialog, ct).ConfigureAwait(true);
        // The binding is the gate on the button; this is the same rule read once more at the moment that matters.
        return result == ContentDialogResult.Primary && viewModel.IsAcknowledged;
    }
}
