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
        ViewModel.SelectionPresented += OnSelectionPresented;
        ViewModel.TrendPresented += OnTrendPresented;
        Unloaded += (_, _) =>
        {
            ViewModel.SelectionPresented -= OnSelectionPresented;
            ViewModel.TrendPresented -= OnTrendPresented;
        };
    }

    public GameDetailViewModel ViewModel { get; }

    private void OnSelectionPresented(object? sender, EventArgs e)
    {
        Frametime.Show(ViewModel.SelectedSeries, displayed: false, sensors: false);
        Distribution.Show(ViewModel.SelectedSeries);
        Sensors.Show(ViewModel.SelectedSeries);
        Latency.Show(ViewModel.SelectedSeries, ViewModel.SelectedSession?.Row.LatencyAvgUs, ViewModel.SelectedSession?.Row.LatencyP95Us);
    }

    private void OnTrendPresented(object? sender, EventArgs e) => Trend.Show(ViewModel.TrendPoints, ViewModel.HardwareChanges, ViewModel.TrendMetricText);

    private void OnSessionDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SessionsGrid.SelectedItem is SessionItemViewModel session)
        {
            ViewModel.OpenSessionCommand.Execute(session);
        }
    }
}
