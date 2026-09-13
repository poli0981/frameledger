using FrameLedger.App.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace FrameLedger.App.Pages;

public partial class SettingsPage : INavigableView<SettingsViewModel>
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = this;
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }
}
