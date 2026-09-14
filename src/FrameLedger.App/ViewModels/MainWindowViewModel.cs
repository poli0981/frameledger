using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.ViewModels;

/// <summary>The shell's state: the Agent pill, the offline banner, and the menu commands that exist in this build.</summary>
public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly AgentConnection _agent;
    private readonly ISnackbarService _snackbar;
    private readonly IShellPresence _shell;
    private readonly IPageNavigator _navigator;
    private readonly UiThread _ui = new();

    [ObservableProperty]
    private string _agentStatusText = Strings.Agent_State_Connecting;

    [ObservableProperty]
    private ControlAppearance _agentStatusAppearance = ControlAppearance.Info;

    [ObservableProperty]
    private bool _isAgentBannerVisible;

    [ObservableProperty]
    private string _agentBannerBody = string.Empty;

    private readonly AddGameFlow _addGame;

    public MainWindowViewModel(AgentConnection agent, ISnackbarService snackbar, IShellPresence shell, IPageNavigator navigator, AddGameFlow addGame, SafetyNotices notices)
    {
        Notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _snackbar = snackbar ?? throw new ArgumentNullException(nameof(snackbar));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _addGame = addGame ?? throw new ArgumentNullException(nameof(addGame));
        _agent.Changed += OnAgentChanged;
        Refresh();
    }

    public static string Title => Strings.App_Title;

    /// <summary>08_UI §Notifications policy: the safety events, as persistent InfoBars above every page — never toasts.</summary>
    public SafetyNotices Notices { get; }

    public void Dispose() => _agent.Changed -= OnAgentChanged;

    private void OnAgentChanged(object? sender, EventArgs e) => _ui.Post(Refresh);

    private void Refresh()
    {
        (string text, ControlAppearance appearance) = AgentStatusPresentation.Pill(_agent.State);
        AgentStatusText = text;
        AgentStatusAppearance = appearance;
        string? banner = AgentStatusPresentation.Banner(_agent.State);
        AgentBannerBody = banner ?? string.Empty;
        IsAgentBannerVisible = banner is not null;
    }

    [RelayCommand]
    private void RetryAgent() => _agent.RetryNow();

    /// <summary>The user read it (on a refusal: acknowledged that the session is recorded without measuring). Nothing else changes — the Agent already did what the notice says.</summary>
    [RelayCommand]
    private void DismissNotice(SafetyNotice notice) => Notices.Dismiss(notice);

    /// <summary>File ▸ Add game… (FR-1.1): the same flow as the Games page's button.</summary>
    [RelayCommand]
    private async Task AddGameAsync() => _ = await _addGame.RunAsync().ConfigureAwait(true);

    /// <summary>Exit is a host shutdown through the shell (App.RunAsync tears down and then ends the process) — a real exit, whatever minimize-to-tray says.</summary>
    [RelayCommand]
    private void Exit() => _shell.Quit();

    /// <summary>Tools ▸ Agent status… is the Dashboard's Agent card.</summary>
    [RelayCommand]
    private void AgentStatus() => _navigator.Navigate<DashboardPage>();

    /// <summary>Tools ▸ Vulkan layer registration… is the Settings page's Capture section.</summary>
    [RelayCommand]
    private void VulkanLayer() => _navigator.Navigate<SettingsPage>();

    [RelayCommand]
    private static void OpenDataFolder()
    {
        try
        {
            using Process? _ = Process.Start(new ProcessStartInfo("explorer.exe", UiPaths.DataDirectory) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Serilog.Log.Warning(ex, "ui: could not open the data folder");
        }
    }

    [RelayCommand]
    [SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
    private static async Task AboutAsync()
    {
        var box = new MessageBox
        {
            Title = Strings.About_Title,
            Content = string.Format(CultureInfo.CurrentCulture, Strings.About_Body_Format, UiIdentity.Version),
            CloseButtonText = Strings.Common_Ok,
        };
        await box.ShowDialogAsync().ConfigureAwait(true);
    }

    /// <summary>Every menu item of <c>08_UI</c> §Menu bar that a later slice builds says so, in a 4 s snackbar, rather than doing nothing.</summary>
    [RelayCommand]
    private void NotYet() =>
        _snackbar.Show(Strings.NotYet_Title, Strings.NotYet_Body, ControlAppearance.Secondary, new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(4));
}
