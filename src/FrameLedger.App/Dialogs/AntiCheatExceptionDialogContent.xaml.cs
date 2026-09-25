using System.Windows.Controls;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Dialogs;

/// <summary>The body of D33's <c>ContentDialog</c>; the buttons are the dialog's own (<see cref="Services.AntiCheatExceptionPrompt"/>).</summary>
public partial class AntiCheatExceptionDialogContent : UserControl
{
    public AntiCheatExceptionDialogContent(AntiCheatExceptionDialogViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => Acceptance.Focus();
    }

    public AntiCheatExceptionDialogViewModel ViewModel { get; }
}
