using Wpf.Ui;

namespace FrameLedger.App.Services;

/// <summary><see cref="IPageNavigator"/> over WPF UI's <see cref="INavigationService"/> — the only thing that navigates (<c>16_WPFUI_SYNTAX</c> §Navigation).</summary>
public sealed class PageNavigator : IPageNavigator
{
    private readonly INavigationService _navigation;
    private readonly ShellHost _shell;

    public PageNavigator(INavigationService navigation, ShellHost shell)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
    }

    public void Navigate<TPage>()
        where TPage : class
    {
        _shell.Remember(typeof(TPage));
        _ = _navigation.Navigate(typeof(TPage));
    }

    public void GoBack() => _navigation.GoBack();
}
