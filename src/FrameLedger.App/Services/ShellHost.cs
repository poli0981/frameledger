using System.Windows;
using FrameLedger.App.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wpf.Ui;

namespace FrameLedger.App.Services;

/// <summary>
/// Owns the live <see cref="MainWindow"/>: shows the first one, rebuilds it on a language change
/// (<c>09_I18N</c> §Mechanics: "re-create the main window shell; state preserved via VM"), and turns the
/// user closing the live one into a host shutdown. The window is a transient service so a rebuild is a fresh
/// XAML load in the new culture; the application's <c>ShutdownMode</c> is explicit so that closing the OLD
/// window of a rebuild does not end the process. With minimize-to-tray on (P3 PR-8b) the user's close hides the
/// window instead, and only <see cref="Quit"/> — the tray's and the File menu's — ends the host.
/// </summary>
public sealed class ShellHost(IServiceProvider services, INavigationService navigation, IHostApplicationLifetime lifetime, WindowClosePolicy policy) : IShellPresence
{
    private MainWindow? _current;
    private Type _page = typeof(DashboardPage);
    private bool _exiting;

    public void Show()
    {
        MainWindow window = services.GetRequiredService<MainWindow>();
        window.Closing += OnClosing;
        window.Closed += OnClosed;
        _current = window;
        window.Show();
        navigation.Navigate(_page);
    }

    public bool IsShown => _current is { IsVisible: true, WindowState: not WindowState.Minimized };

    public void Reveal()
    {
        if (_current is null)
        {
            Show();
            return;
        }

        _current.Show();
        if (_current.WindowState == WindowState.Minimized)
        {
            _current.WindowState = WindowState.Normal;
        }

        _current.Activate();
    }

    public void Quit()
    {
        _exiting = true;
        lifetime.StopApplication();
    }

    /// <summary>The page the shell will open on, kept across a rebuild.</summary>
    public void Remember(Type page) => _page = page;

    /// <summary>A new window in the current UI culture; the old one closes after the new one is up.</summary>
    public void Rebuild()
    {
        MainWindow? old = _current;
        Show();
        old?.Close();
    }

    public Window? Current => _current;

    /// <summary>FR-10 minimize-to-tray: the user's close of the live window hides it while a tray icon exists to come back from.</summary>
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_exiting && ReferenceEquals(sender, _current) && policy.HidesOnClose && sender is Window window)
        {
            e.Cancel = true;
            window.Hide();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        // Only the live window's close is the user leaving; a rebuild's old window is not.
        if (ReferenceEquals(sender, _current))
        {
            _current = null;
            lifetime.StopApplication();
        }
    }
}
