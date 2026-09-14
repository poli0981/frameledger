using System.Windows.Controls;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Controls;

/// <summary>The first-run steps as one control, so a test renders it without a window.</summary>
public partial class FirstRunContent : UserControl
{
    public FirstRunContent(FirstRunViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = this;
        InitializeComponent();
    }

    public FirstRunViewModel ViewModel { get; }
}
