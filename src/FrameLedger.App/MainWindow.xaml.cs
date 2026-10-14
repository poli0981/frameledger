// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;
using System.Windows.Threading;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using Serilog;
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
        TimeNavigations();
    }

    /// <summary>
    /// beta.23 (<c>spike-notes</c> §21): how long each page takes to be on screen once WPF UI navigates to it — its layout,
    /// its templates, its first render — logged at Debug (Settings ▸ Logging), so a slow page on a real PC says which it is.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "a WPF application has no JoinableTaskFactory; this posts to the window's own dispatcher from its own thread, at the priority after rendering, and never waits (ScrollBarHiding does the same)")]
    private void TimeNavigations()
    {
        long navigating = 0;
        RootNavigation.Navigating += (_, _) => navigating = Stopwatch.GetTimestamp();
        RootNavigation.Navigated += (_, e) =>
        {
            long from = navigating;
            string page = e.Page?.GetType().Name ?? "?";
            DispatcherOperation logged = Dispatcher.InvokeAsync(
                () => Log.Debug("ui: page {Page} shown {Ms:0} ms after its navigation", page, Stopwatch.GetElapsedTime(from).TotalMilliseconds),
                DispatcherPriority.ContextIdle);
            GC.KeepAlive(logged);
        };
    }

    public MainWindowViewModel ViewModel { get; }
}
