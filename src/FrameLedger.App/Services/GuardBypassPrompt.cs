using System.Windows.Data;
using FrameLedger.App.Dialogs;
using FrameLedger.App.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>
/// <see cref="IGuardBypassPrompt"/> as a <c>ContentDialog</c>, the shape FR-2.1's consent dialog has and stricter: the
/// primary button is the Danger one, it is enabled only while the checkbox is ticked AND the phrase is typed, and Enter
/// keeps the guard on.
/// </summary>
public sealed class GuardBypassPrompt : IGuardBypassPrompt
{
    private readonly IContentDialogService _dialogs;

    public GuardBypassPrompt(IContentDialogService dialogs) => _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    public async Task<bool> ShowAsync(string gameName, string? finding, CancellationToken ct = default)
    {
        var viewModel = new GuardBypassDialogViewModel(gameName, finding);
        var dialog = new ContentDialog
        {
            Title = viewModel.Title,
            Content = new GuardBypassDialogContent(viewModel),
            PrimaryButtonText = GuardBypassDialogViewModel.ConfirmText,
            PrimaryButtonAppearance = ControlAppearance.Danger,
            CloseButtonText = GuardBypassDialogViewModel.KeepGuardText,
            // Enter keeps the guard on; only a click on the (doubly gated) primary overrules it.
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            DialogMaxWidth = 720,
        };
        _ = dialog.SetBinding(ContentDialog.IsPrimaryButtonEnabledProperty, new Binding(nameof(GuardBypassDialogViewModel.IsConfirmed)) { Source = viewModel, Mode = BindingMode.OneWay });

        ContentDialogResult result = await _dialogs.ShowAsync(dialog, ct).ConfigureAwait(true);
        // The binding is the gate on the button; this is the same rule read once more at the moment that matters.
        return result == ContentDialogResult.Primary && viewModel.IsConfirmed;
    }
}
