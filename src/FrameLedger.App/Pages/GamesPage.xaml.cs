using System.Windows;
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

    /// <summary>FR-1.1's drop: files only (a shortcut's target needs the shell link reader, later).</summary>
    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            _ = ViewModel.DropAsync(paths);
        }
    }
}
