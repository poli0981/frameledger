using FrameLedger.App.Dialogs;
using FrameLedger.App.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Services;

/// <summary>Step 3's preview in WPF UI: a <c>ContentDialog</c> listing the zip's entries, whose primary button is step 4 and whose secondary is the clipboard fallback.</summary>
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
}
