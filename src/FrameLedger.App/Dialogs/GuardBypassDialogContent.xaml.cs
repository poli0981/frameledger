using System.Windows.Controls;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Dialogs;

public partial class GuardBypassDialogContent : UserControl
{
    public GuardBypassDialogContent(GuardBypassDialogViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        InitializeComponent();
    }

    public GuardBypassDialogViewModel ViewModel { get; }
}
