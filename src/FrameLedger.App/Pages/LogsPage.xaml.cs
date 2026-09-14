using System.Windows.Controls;
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

        // The tail refreshes only while the page is on screen.
        Loaded += (_, _) => ViewModel.Start();
        Unloaded += (_, _) => ViewModel.Stop();
    }

    public LogsViewModel ViewModel { get; }

    /// <summary>Autoscroll unless paused (08_UI §Logs "pause-scroll").</summary>
    private void OnTailChanged(object sender, TextChangedEventArgs e)
    {
        if (!ViewModel.IsPaused)
        {
            Tail.ScrollToEnd();
        }
    }
}
