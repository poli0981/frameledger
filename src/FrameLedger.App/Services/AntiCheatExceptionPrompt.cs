using System.Windows.Data;
using FrameLedger.App.Dialogs;
using FrameLedger.App.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>
/// D33's disclosure in WPF UI, the same shape as FR-2.1's (<see cref="ConsentPrompt"/>): the primary button is bound to the
/// ticked acceptance and disabled until it is ticked; the close button keeps the game blocked and is the default, so Enter
/// never makes an exception.
/// </summary>
public sealed class AntiCheatExceptionPrompt : IAntiCheatExceptionPrompt
{
    private readonly IContentDialogService _dialogs;

    public AntiCheatExceptionPrompt(IContentDialogService dialogs) => _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    public async Task<bool> ShowAsync(AntiCheatExceptionFacts facts, CancellationToken ct = default)
    {
        var viewModel = new AntiCheatExceptionDialogViewModel(facts);
        var dialog = new ContentDialog
        {
            Title = viewModel.Title,
            Content = new AntiCheatExceptionDialogContent(viewModel),
            PrimaryButtonText = AntiCheatExceptionDialogViewModel.GrantText,
            CloseButtonText = AntiCheatExceptionDialogViewModel.CancelText,
            // Enter keeps the game blocked; only a click on the (acceptance-gated) primary makes the exception.
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            DialogMaxWidth = 700,
        };
        _ = dialog.SetBinding(ContentDialog.IsPrimaryButtonEnabledProperty,
            new Binding(nameof(AntiCheatExceptionDialogViewModel.Accepted)) { Source = viewModel, Mode = BindingMode.OneWay });

        ContentDialogResult result = await _dialogs.ShowAsync(dialog, ct).ConfigureAwait(true);
        // The binding is the gate on the button; this is the same rule read once more at the moment that matters.
        return result == ContentDialogResult.Primary && viewModel.Accepted;
    }
}
