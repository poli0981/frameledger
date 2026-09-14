using System.ComponentModel;
using System.Windows;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.Windows;

/// <summary>
/// The session summary as a secondary <see cref="FluentWindow"/> (<c>08_UI</c> §Session summary). It owns a
/// <c>ContentDialogService</c> of its own for FR-8.3's dialog (the shell's is bound to the shell), applies the
/// theme before its XAML like the shell does, and redraws the charts whenever the view model presents.
/// </summary>
public partial class SessionSummaryWindow : FluentWindow
{
    private readonly ContentDialogService _dialogs = new();

    public SessionSummaryWindow(SessionSummaryViewModel viewModel, IThemeApplier theme, AppearanceSettings appearance)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(appearance);
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = this;
        theme.Apply(appearance.Theme, null);
        InitializeComponent();
        _dialogs.SetDialogHost(DialogHost);
        ViewModel.Presented += OnPresented;
        ViewModel.PropertyChanged += OnViewModelChanged;
        Closed += (_, _) =>
        {
            ViewModel.Presented -= OnPresented;
            ViewModel.PropertyChanged -= OnViewModelChanged;
        };
    }

    public SessionSummaryViewModel ViewModel { get; }

    /// <summary>The dialog service bound to this window's host, for the prompts the view model was built with.</summary>
    public IContentDialogService Dialogs => _dialogs;

    private void OnPresented(object? sender, EventArgs e) => Redraw();

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SessionSummaryViewModel.ShowDisplayed):
                Frametime.SetDisplayed(ViewModel.ShowDisplayed);
                break;
            case nameof(SessionSummaryViewModel.ShowSensors):
                Frametime.SetSensors(ViewModel.ShowSensors);
                break;
            default:
                break;
        }
    }

    private void Redraw()
    {
        Frametime.Show(ViewModel.Series, ViewModel.ShowDisplayed, ViewModel.ShowSensors);
        Distribution.Show(ViewModel.Series);
    }

    private void OnExportPng(object sender, RoutedEventArgs e) => _ = ViewModel.ExportPngAsync(Frametime.ScottPlot);
}
