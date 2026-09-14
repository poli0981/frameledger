using System.Windows.Controls;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Dialogs;

/// <summary>The body of FR-8.3's override <c>ContentDialog</c>.</summary>
public partial class TriStateOverrideContent : UserControl
{
    public TriStateOverrideContent(TriStateOverrideViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        InitializeComponent();
    }

    public TriStateOverrideViewModel ViewModel { get; }
}
