using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Services;
using Microsoft.Extensions.Hosting;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace FrameLedger.App.ViewModels;

/// <summary>The shell's state: the Agent pill, the offline banner, and the menu commands that exist in this build.</summary>
public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly AgentConnection _agent;
    private readonly ISnackbarService _snackbar;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly UiThread _ui = new();

    [ObservableProperty]
    private string _agentStatusText = Strings.Agent_State_Connecting;

    [ObservableProperty]
    private ControlAppearance _agentStatusAppearance = ControlAppearance.Info;

    [ObservableProperty]
    private bool _isAgentBannerVisible;

    [ObservableProperty]
    private string _agentBannerBody = string.Empty;

    public MainWindowViewModel(AgentConnection agent, ISnackbarService snackbar, IHostApplicationLifetime lifetime)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _snackbar = snackbar ?? throw new ArgumentNullException(nameof(snackbar));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        _agent.Changed += OnAgentChanged;
        Refresh();
    }

    public static string Title => Strings.App_Title;

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

    /// <summary>Exit is a host shutdown (App.RunAsync tears down and then ends the process), not a bare <c>Application.Shutdown</c>.</summary>
    [RelayCommand]
    private void Exit() => _lifetime.StopApplication();

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
