using System.Windows.Input;
using FrameLedger.App.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace FrameLedger.App.Pages;

/// <summary>The game page; which game is <c>GameSelection</c>'s, read by the view model.</summary>
public partial class GameDetailPage : INavigableView<GameDetailViewModel>
{
    public GameDetailPage(GameDetailViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = this;
        InitializeComponent();
    }

    public GameDetailViewModel ViewModel { get; }

    private void OnSessionDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SessionsGrid.SelectedItem is SessionItemViewModel session)
        {
            ViewModel.OpenSessionCommand.Execute(session);
        }
    }
}
