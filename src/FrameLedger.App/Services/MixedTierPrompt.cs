using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>FR-6.2's guard as a <c>ContentDialog</c> on the shell's host: the explanation, then "Compare across tiers anyway" or Cancel.</summary>
public sealed class MixedTierPrompt : IMixedTierPrompt
{
    private readonly IContentDialogService _dialogs;

    public MixedTierPrompt(IContentDialogService dialogs) => _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    public async Task<bool> AcknowledgeAsync(CancellationToken ct = default)
    {
        var dialog = new ContentDialog
        {
            Title = Strings.Compare_Mixed_Title,
            Content = new TextBlock { Text = Strings.Compare_Mixed_Body, TextWrapping = System.Windows.TextWrapping.Wrap, MaxWidth = 560 },
            PrimaryButtonText = Strings.Compare_Mixed_Proceed,
            CloseButtonText = Strings.Common_Cancel,
            DefaultButton = ContentDialogButton.Close,
        };
        return await _dialogs.ShowAsync(dialog, ct).ConfigureAwait(true) == ContentDialogResult.Primary;
    }
}
