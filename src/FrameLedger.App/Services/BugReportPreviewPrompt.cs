using FrameLedger.App.Dialogs;
using FrameLedger.App.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>
/// The bug report's dialogs in WPF UI: step 3's <c>ContentDialog</c> listing the zip's entries, whose primary button is
/// step 4 and whose secondary is the clipboard fallback; and step 2's optional items, a checkbox each.
/// </summary>
public sealed class BugReportPreviewPrompt : IBugReportPreview
{
    private readonly IContentDialogService _dialogs;

    public BugReportPreviewPrompt(IContentDialogService dialogs) => _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    public async Task<BugReportChoice> ShowAsync(BugReportPreviewModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        var dialog = new ContentDialog
        {
            Title = Strings.BugReport_Title,
            Content = new BugReportPreviewContent(new BugReportPreviewViewModel(model)),
            PrimaryButtonText = Strings.BugReport_OpenIssue,
            SecondaryButtonText = Strings.BugReport_CopyMarkdown,
            CloseButtonText = Strings.Common_Close,
            DefaultButton = ContentDialogButton.Primary,
            DialogMaxWidth = 720,
        };

        ContentDialogResult result = await _dialogs.ShowAsync(dialog, ct).ConfigureAwait(true);
        return result switch
        {
            ContentDialogResult.Primary => BugReportChoice.OpenIssue,
            ContentDialogResult.Secondary => BugReportChoice.CopyMarkdown,
            _ => BugReportChoice.Close,
        };
    }

    public async Task<BugBundleOptions> AskOptionsAsync(BugBundleOffer offer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(offer);
        var viewModel = new BugBundleOptionsViewModel(offer);
        var dialog = new ContentDialog
        {
            Title = BugBundleOptionsViewModel.Title,
            Content = new BugBundleOptionsContent(viewModel),
            PrimaryButtonText = Strings.BugReport_Options_Continue,
            CloseButtonText = Strings.Common_Cancel,
            DefaultButton = ContentDialogButton.Primary,
            DialogMaxWidth = 640,
        };

        ContentDialogResult result = await _dialogs.ShowAsync(dialog, ct).ConfigureAwait(true);
        return viewModel.Options(result == ContentDialogResult.Primary);
    }
}
