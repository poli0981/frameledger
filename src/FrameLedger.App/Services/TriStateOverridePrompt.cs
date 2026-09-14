using FrameLedger.App.Dialogs;
using FrameLedger.App.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>
/// FR-8.3's dialog in WPF UI over a dialog service the summary window owns (each secondary window has its own
/// <c>ContentDialogHost</c>; the shell's service is bound to the shell).
/// </summary>
public sealed class TriStateOverridePrompt : ITriStateOverridePrompt
{
    private readonly Func<IContentDialogService> _dialogs;

    /// <summary>The service is resolved per ask: the window that owns the host exists by then.</summary>
    public TriStateOverridePrompt(Func<IContentDialogService> dialogs) => _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    public async Task<TriStateOverrideChoice?> AskAsync(TriStateChipModel chip, CancellationToken ct = default)
    {
        var viewModel = new TriStateOverrideViewModel(chip);
        var dialog = new ContentDialog
        {
            Title = viewModel.Title,
            Content = new TriStateOverrideContent(viewModel),
            PrimaryButtonText = TriStateOverrideViewModel.ApplyText,
            CloseButtonText = Strings.Common_Cancel,
            DefaultButton = ContentDialogButton.Primary,
        };
        ContentDialogResult result = await _dialogs().ShowAsync(dialog, ct).ConfigureAwait(true);
        return result == ContentDialogResult.Primary ? viewModel.Choice() : null;
    }
}
