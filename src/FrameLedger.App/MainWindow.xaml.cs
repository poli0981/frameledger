using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace FrameLedger.App;

/// <summary>
/// The shell (P3 PR-2, <c>docs/08_UI.md</c> §Shell): wires the three WPF UI services to its own controls once,
/// applies the theme (and with it the accent resources) before its XAML loads, and re-applies it from
/// <c>Loaded</c> so the System watcher gets its HWND (<c>16_WPFUI_SYNTAX</c> §Gotchas).
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow(
        MainWindowViewModel viewModel,
        INavigationService navigation,
        ISnackbarService snackbar,
        IContentDialogService dialogs,
        INavigationViewPageProvider pages,
        IThemeApplier theme,
        AppearanceSettings appearance)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(snackbar);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(appearance);

        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = this;

        // Before the XAML loads: ApplicationThemeManager.Apply(…, updateAccent: true) is what registers the
        // SystemAccentColor* resources the control templates StaticResource at first Measure (measured 2026-09-13:
        // without it the first Show() throws XamlParseException "Cannot find resource named
        // 'SystemAccentColorPrimary'"). The watcher half needs the HWND and runs from Loaded.
        theme.Apply(appearance.Theme, null);
        InitializeComponent();

        RootNavigation.SetPageProviderService(pages);
        navigation.SetNavigationControl(RootNavigation);
        snackbar.SetSnackbarPresenter(SnackbarPresenter);
        dialogs.SetDialogHost(DialogHost);

        Loaded += (_, _) => theme.Apply(appearance.Theme, this);
    }

    public MainWindowViewModel ViewModel { get; }
}
