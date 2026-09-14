using FrameLedger.App.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace FrameLedger.App.Pages;

public partial class GamesPage : INavigableView<GamesViewModel>
{
    public GamesPage(GamesViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = this;
        InitializeComponent();
    }

    public GamesViewModel ViewModel { get; }
}
