using FrameLedger.App.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace FrameLedger.App.Pages;

public partial class LogsPage : INavigableView<LogsViewModel>
{
    public LogsPage(LogsViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = this;
        InitializeComponent();
    }

    public LogsViewModel ViewModel { get; }
}
