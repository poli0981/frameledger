using System.Windows.Controls;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Dialogs;

/// <summary>The body of FR-1.2's review <c>ContentDialog</c> (P4 PR-4).</summary>
public partial class ImportReviewContent : UserControl
{
    public ImportReviewContent(ImportReviewViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        InitializeComponent();
    }

    public ImportReviewViewModel ViewModel { get; }
}
