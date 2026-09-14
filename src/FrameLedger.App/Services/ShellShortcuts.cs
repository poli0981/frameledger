using FrameLedger.App.Pages;

namespace FrameLedger.App.Services;

/// <summary>
/// <c>08_UI</c> §Accessibility, keyboard: Ctrl+1 … Ctrl+5 (and the numeric keypad) open the NavigationView's pages in
/// the order the shell lists them — Dashboard, Games, Compare, Logs, Settings. The key bindings live in
/// <c>MainWindow.xaml</c>; this is the one place that knows which number is which page, and a test holds the two
/// orders together.
/// </summary>
public static class ShellShortcuts
{
    /// <summary>Navigates for <paramref name="key"/> ("1" … "5"); false for anything else, which does nothing.</summary>
    public static bool Navigate(IPageNavigator navigator, string? key)
    {
        ArgumentNullException.ThrowIfNull(navigator);
        switch (key)
        {
            case "1":
                navigator.Navigate<DashboardPage>();
                return true;
            case "2":
                navigator.Navigate<GamesPage>();
                return true;
            case "3":
                navigator.Navigate<ComparePage>();
                return true;
            case "4":
                navigator.Navigate<LogsPage>();
                return true;
            case "5":
                navigator.Navigate<SettingsPage>();
                return true;
            default:
                return false;
        }
    }
}
