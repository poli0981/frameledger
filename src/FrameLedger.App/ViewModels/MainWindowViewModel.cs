using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using FrameLedger.App.Update;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Shared.Ipc;
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

    /// <summary>08_UI §Notifications policy: "update downloaded" is in-app and persistent — the banner follows <see cref="UpdateService.Stage"/>.</summary>
    [ObservableProperty]
    private bool _isUpdateBannerVisible;

    [ObservableProperty]
    private string _updateBannerBody = string.Empty;

    /// <summary>FR-12: the button is live only while no session runs; deferred, it stays visible and disabled with the body saying why.</summary>
    [ObservableProperty]
    private bool _canRestartToUpdate;

    private readonly AddGameFlow _addGame;
    private readonly UpdateService _updates;
    private readonly IDatabaseMaintenancePrompts _maintenance;
    private readonly BugReportFlow _bugReports;
    private readonly IUrlOpener _urls;
    private readonly SessionSelection _selection;
    private readonly SessionExportService _exports;
    private readonly ISessionSummaryOpener _summaries;
    private readonly ImportLibraryFlow _import;

    public MainWindowViewModel(AgentConnection agent, ISnackbarService snackbar, IShellPresence shell, IPageNavigator navigator, AddGameFlow addGame, SafetyNotices notices,
        BugReportFlow bugReports, IUrlOpener urls, SessionSelection selection, SessionExportService exports, ISessionSummaryOpener summaries, ImportLibraryFlow import,
        UpdateService updates, IDatabaseMaintenancePrompts maintenance)
    {
        _maintenance = maintenance ?? throw new ArgumentNullException(nameof(maintenance));
        Notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _updates = updates ?? throw new ArgumentNullException(nameof(updates));
        _import = import ?? throw new ArgumentNullException(nameof(import));
        _bugReports = bugReports ?? throw new ArgumentNullException(nameof(bugReports));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _exports = exports ?? throw new ArgumentNullException(nameof(exports));
        _summaries = summaries ?? throw new ArgumentNullException(nameof(summaries));
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _snackbar = snackbar ?? throw new ArgumentNullException(nameof(snackbar));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _addGame = addGame ?? throw new ArgumentNullException(nameof(addGame));
        _agent.Changed += OnAgentChanged;
        _updates.PropertyChanged += OnUpdatesChanged;
        Refresh();
        RefreshUpdate();
    }

    public static string Title => Strings.App_Title;

    /// <summary>08_UI §Notifications policy: the safety events, as persistent InfoBars above every page — never toasts.</summary>
    public SafetyNotices Notices { get; }

    public void Dispose()
    {
        _agent.Changed -= OnAgentChanged;
        _updates.PropertyChanged -= OnUpdatesChanged;
    }

    private void OnAgentChanged(object? sender, EventArgs e) => _ui.Post(Refresh);

    private void OnUpdatesChanged(object? sender, PropertyChangedEventArgs e) => RefreshUpdate();

    /// <summary>The banner's three faces: downloading (with the percent), ready (the button live), deferred (FR-12, the button waits).</summary>
    [SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
    private void RefreshUpdate()
    {
        string version = _updates.Version ?? string.Empty;
        (IsUpdateBannerVisible, UpdateBannerBody, CanRestartToUpdate) = _updates.Stage switch
        {
            UpdateStage.Downloading => (true, string.Format(CultureInfo.CurrentCulture, Strings.Update_Banner_Downloading_Format, version, _updates.Percent), false),
            UpdateStage.Ready => (true, string.Format(CultureInfo.CurrentCulture, Strings.Update_Banner_Ready_Format, version), true),
            UpdateStage.Deferred => (true, string.Format(CultureInfo.CurrentCulture, Strings.Update_Banner_Deferred_Format, version), false),
            UpdateStage.Applying => (true, string.Format(CultureInfo.CurrentCulture, Strings.Update_Banner_Applying_Format, version), false),
            _ => (false, string.Empty, false),
        };
    }

    /// <summary>Help ▸ Check for updates (P4 PR-5): <c>11_UPDATER</c> §Flow, "Manual check".</summary>
    [RelayCommand]
    private Task CheckForUpdatesAsync() => _updates.CheckInteractivelyAsync();

    /// <summary>The banner's button: the Agent stopped, the updater told to wait for this process, the host ended (FR-12 is checked again inside).</summary>
    [RelayCommand]
    private Task RestartToUpdateAsync() => _updates.RestartToUpdateAsync();

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

    /// <summary>
    /// Tools ▸ Update detection rules (P4 PR-1): the Agent re-seeds the rules file and wakes its detection sweep, so every
    /// game's engine / platform / Supports row follows within the second (<c>05_DETECTION</c> §Caching). The ack's
    /// outcome is the seeder's word — <c>AlreadyCurrent</c>, <c>Updated</c>, … — shown as is; there is no fetch yet.
    /// </summary>
    [RelayCommand]
    [SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
    private async Task UpdateRulesAsync()
    {
        if (!_agent.IsConnected)
        {
            _snackbar.Show(Strings.Rules_Update_Title, Strings.Rules_Update_NoAgent, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Warning24), TimeSpan.FromSeconds(4));
            return;
        }

        try
        {
            IpcEnvelope ack = await _agent.RequestAsync(IpcMessageType.UpdateRules, new UpdateRulesRequest()).ConfigureAwait(true);
            string outcome = IpcCodec.Payload<UpdateRulesAck>(ack)?.Outcome ?? string.Empty;
            _snackbar.Show(Strings.Rules_Update_Title, string.Format(CultureInfo.CurrentCulture, Strings.Rules_Update_Done_Format, outcome),
                ControlAppearance.Secondary, new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(4));
        }
        catch (Exception ex) when (ex is IpcRequestException or TimeoutException or System.IO.IOException or InvalidOperationException)
        {
            Serilog.Log.Warning(ex, "ui: update rules did not complete");
            _snackbar.Show(Strings.Rules_Update_Title, Strings.Rules_Update_Failed, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Warning24), TimeSpan.FromSeconds(4));
        }
    }

    /// <summary>File ▸ Import library… (FR-1.2, P4 PR-4): the stores' installed titles through the review checklist; hooking off for every row added.</summary>
    [RelayCommand]
    private async Task ImportLibraryAsync() => _ = await _import.RunAsync().ConfigureAwait(true);

    /// <summary>Help ▸ Documentation (P4 PR-3): the README is the user-facing entry point; `docs/` is the developers'.</summary>
    [RelayCommand]
    private void Documentation()
    {
        if (!_urls.Open(IssueLink.Documentation))
        {
            _snackbar.Show(Strings.Menu_Help_Documentation, Strings.BugReport_BrowserRefused, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Warning24), TimeSpan.FromSeconds(4));
        }
    }

    /// <summary>Help ▸ Report a bug… (P4 PR-3): <c>10_LOGGING</c> §Bug report flow steps 2–4, the same flow the Logs page's button runs.</summary>
    [RelayCommand]
    private async Task ReportBugAsync() => _ = await _bugReports.RunAsync().ConfigureAwait(true);

    /// <summary>File ▸ Export ▸ Session CSV, for the session last selected on a Sessions tab or opened in a summary window.</summary>
    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (_selection.SessionId is { } id)
        {
            _ = await _exports.ExportCsvAsync(id).ConfigureAwait(true);
        }
        else
        {
            NoSelection();
        }
    }

    [RelayCommand]
    private async Task ExportJsonAsync()
    {
        if (_selection.SessionId is { } id)
        {
            _ = await _exports.ExportJsonAsync(id).ConfigureAwait(true);
        }
        else
        {
            NoSelection();
        }
    }

    /// <summary>File ▸ Export ▸ Chart PNG needs the summary window's live plot, so it opens that window and says so.</summary>
    [RelayCommand]
    private void ExportPng()
    {
        if (_selection.SessionId is { } id)
        {
            _summaries.Open(id);
            _snackbar.Show(Strings.Menu_File_Export, Strings.Export_Png_Body, ControlAppearance.Secondary, new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(4));
        }
        else
        {
            NoSelection();
        }
    }

    private void NoSelection() =>
        _snackbar.Show(Strings.Menu_File_Export, Strings.Export_NoSelection_Body, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(4));

    /// <summary>
    /// Tools ▸ Database maintenance… (P4 PR-7): integrity check, backup, the retention sweep (the Agent's) and compaction.
    /// It was the last menu item that said "not built yet"; the placeholder command went with it.
    /// </summary>
    [RelayCommand]
    private Task DatabaseMaintenanceAsync() => _maintenance.ShowAsync();
}
