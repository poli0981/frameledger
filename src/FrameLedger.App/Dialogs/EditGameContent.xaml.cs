using System.Windows.Controls;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Dialogs;

/// <summary>The body of FR-1.3's edit <c>ContentDialog</c>.</summary>
public partial class EditGameContent : UserControl
{
    public EditGameContent(EditGameViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        InitializeComponent();
    }

    public EditGameViewModel ViewModel { get; }
}
