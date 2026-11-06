// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

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
    private long _handle;

    /// <summary>
    /// The live window's handle, for Windows' administrator prompt to belong to (beta.10, the admin mode); 0 before the first
    /// window. Written on the UI thread when a window is shown, read from the Agent connection's thread.
    /// </summary>
    public nint Handle => (nint)Interlocked.Read(ref _handle);

    public void Show()
    {
        MainWindow window = services.GetRequiredService<MainWindow>();
        window.Closing += OnClosing;
        window.Closed += OnClosed;
        // beta.34 (owner request 2026-11-06): every page the window shows is the one a rebuild opens on — a click in the pane
        // included, which never went through PageNavigator. A language changed on Settings stays on Settings; it came back
        // on the last page opened from code (a game's page, typically).
        window.RootNavigation.Navigated += (_, e) => NoteShown(ReferenceEquals(window, _current), e.Page);
        _current = window;
        window.Show();
        Interlocked.Exchange(ref _handle, new System.Windows.Interop.WindowInteropHelper(window).Handle);
        navigation.Navigate(_page);
        // NFR-4 "cold start ≤ 2 s": measured from the process's own start, printed every run so a regression is a log line.
        Serilog.Log.Information("ui: shell shown {Ms} ms after process start", (long)(DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalMilliseconds);
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

    /// <summary>The page a rebuild opens on (beta.34: the page on screen).</summary>
    internal Type Page => _page;

    /// <summary>
    /// A page the live window shows (beta.34): remembered whichever way it was reached — the pane, the keyboard, a link, code.
    /// The old window of a rebuild, closing, changes nothing.
    /// </summary>
    internal void NoteShown(bool live, object? page)
    {
        if (live && page is not null)
        {
            _page = page.GetType();
        }
    }

    /// <summary>
    /// A new window in the current UI culture; the old one closes after the new one is up. The long-lived pieces that
    /// read their text once — the tray's menu, the Agent pill — read it again (<see cref="ILanguageFollower"/>, beta.15).
    /// </summary>
    public void Rebuild()
    {
        MainWindow? old = _current;
        Show();
        old?.Close();
        foreach (ILanguageFollower follower in services.GetServices<ILanguageFollower>())
        {
            follower.FollowLanguage();
        }
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
