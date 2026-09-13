using FrameLedger.App.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace FrameLedger.App.Pages;

public partial class DashboardPage : INavigableView<DashboardViewModel>
{
    public DashboardPage(DashboardViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = this;
        InitializeComponent();
    }

    public DashboardViewModel ViewModel { get; }
}
