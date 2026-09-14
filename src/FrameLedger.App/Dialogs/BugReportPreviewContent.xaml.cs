using System.Windows.Controls;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Dialogs;

/// <summary>The body of the bug report's preview <c>ContentDialog</c> (P4 PR-3).</summary>
public partial class BugReportPreviewContent : UserControl
{
    public BugReportPreviewContent(BugReportPreviewViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        InitializeComponent();
    }

    public BugReportPreviewViewModel ViewModel { get; }
}
