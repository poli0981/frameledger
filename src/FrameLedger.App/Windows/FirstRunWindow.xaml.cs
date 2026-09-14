using FrameLedger.App.Controls;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Windows;

/// <summary>The first-run frame: the theme before its XAML like the shell, the steps control inside, and a close-without-decision that the view model reads as a decline.</summary>
public partial class FirstRunWindow : FluentWindow
{
    public FirstRunWindow(FirstRunViewModel viewModel, IThemeApplier theme, AppearanceSettings appearance)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(appearance);
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        theme.Apply(appearance.Theme, null);
        InitializeComponent();
        Host.Content = new FirstRunContent(viewModel);
        Closed += (_, _) => ViewModel.Abandon();
    }

    public FirstRunViewModel ViewModel { get; }
}
