using System.Windows.Controls;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Dialogs;

/// <summary>The body of the bug report's optional-items <c>ContentDialog</c> (P4 PR-9): the crash dump's checkbox.</summary>
public partial class BugBundleOptionsContent : UserControl
{
    public BugBundleOptionsContent(BugBundleOptionsViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        InitializeComponent();
    }

    public BugBundleOptionsViewModel ViewModel { get; }
}
