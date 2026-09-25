using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Settings;
using FrameLedger.Infrastructure.Startup;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// Settings (08_UI §Settings, FR-10) over the registry (<c>06_DATA_MODEL</c> §Settings registry): Capture first —
/// the global kill switch, the hook-enabled games with a per-game revoke (through the pipe, like the game page's
/// toggle: the App never writes a hook-state column), the Vulkan layer's state as the Agent reported it — then
/// recording policy, the Agent section, window, privacy, updates, logging, legal. Theme applies at once and
/// persists; language persists and rebuilds the shell (09_I18N §Mechanics). Every write is one settings row;
/// the Agent reads its keys at the next session start (D16), which is what the snackbar says.
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed partial class SettingsViewModel : ObservableObject
{
    /// <summary>The safety document (<c>19_SAFETY_AND_ANTICHEAT.md</c>) at the project's public repository — opened by the user's browser, never fetched.</summary>
    public const string SafetyDocsUrl = "https://github.com/poli0981/frameledger/blob/main/docs/19_SAFETY_AND_ANTICHEAT.md";

    private readonly AppearanceSettings _appearance;
    private readonly IThemeApplier _themes;
    private readonly ShellHost _shell;
    private readonly RegisteredSettings _settings;
    private readonly GameLibrary _library;
    private readonly HookingConsent _consent;
    private readonly IAgentLink _agent;
    private readonly IRunAtLogon _runAtLogon;
    private readonly IMessageStrip _strip;
    private readonly IMaintenanceState _maintenance;
    private readonly IAgentTool _tool;
    private readonly WindowClosePolicy _closePolicy;
    private readonly IFirstRunFlow _firstRun;
    private readonly AntiCheatExceptions? _exceptions;
    private bool _loading = true;

    [ObservableProperty]
    private AppTheme _selectedTheme;

    [ObservableProperty]
    private string _selectedLanguage = "en";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KillSwitchState))]
    private bool _killSwitch;

    [ObservableProperty]
    private int _minSessionSeconds;

    [ObservableProperty]
    private int _telemetryIntervalMs;

    [ObservableProperty]
    private int _retentionRawSessions;

    [ObservableProperty]
    private bool _backgroundCapture;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _minimizeToTray;

    /// <summary><c>ui.hide_anticheat_hooking</c> (beta.8): an anti-cheat game's page shows the finding instead of its Hooking card.</summary>
    [ObservableProperty]
    private bool _hideAntiCheatHooking = true;

    /// <summary>
    /// <c>hooking.usermode_ac_exceptions</c> (D33, owner decision 2026-09-26): whether a game's user-mode anti-cheat exception
    /// may apply at all. Off by default; off suspends every exception below without deleting any.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExceptionsSuspended))]
    private bool _userModeExceptions;

    /// <summary>D33: the Agent has not answered about every blocked game yet (the option was just turned on).</summary>
    [ObservableProperty]
    private bool _exceptionsChecking;

    /// <summary><c>ui.fps_decimals</c> (beta.8): every FPS figure with two decimals ("62.40") instead of a whole number.</summary>
    [ObservableProperty]
    private bool _fpsTwoDecimals;

    [ObservableProperty]
    private bool _onlineMetadata;

    [ObservableProperty]
    private string _updateChannel = "stable";

    /// <summary><c>11_UPDATER</c> §Flow: the startup silent check, on by default (P4 PR-5).</summary>
    [ObservableProperty]
    private bool _autoCheckUpdates = true;

    [ObservableProperty]
    private bool _logDebug;

    [ObservableProperty]
    private string _vulkanLayerText = Strings.Settings_VkLayer_Unknown;

    [ObservableProperty]
    private string _elevationText = Strings.Settings_VkLayer_Unknown;

    [ObservableProperty]
    private string _taskText = Strings.Settings_Agent_Task_Unknown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRegisterLayer))]
    [NotifyPropertyChangedFor(nameof(CanUnregisterLayer))]
    private MaintenanceSnapshot _maintenanceSnapshot = new(false, false, LogonTaskState.Unknown);

    [ObservableProperty]
    private bool _isToolRunning;

    public SettingsViewModel(
        AppearanceSettings appearance,
        IThemeApplier theme,
        ShellHost shell,
        RegisteredSettings settings,
        GameLibrary library,
        HookingConsent consent,
        IAgentLink agent,
        IRunAtLogon runAtLogon,
        IMessageStrip strip,
        IMaintenanceState maintenance,
        IAgentTool tool,
        WindowClosePolicy closePolicy,
        IFirstRunFlow firstRun,
        AntiCheatExceptions? exceptions = null)
    {
        // D33: the user-mode exception's requests; a composition without it (a test) answers that the Agent is unavailable.
        _exceptions = exceptions;
        _firstRun = firstRun ?? throw new ArgumentNullException(nameof(firstRun));
        _maintenance = maintenance ?? throw new ArgumentNullException(nameof(maintenance));
        _tool = tool ?? throw new ArgumentNullException(nameof(tool));
        _closePolicy = closePolicy ?? throw new ArgumentNullException(nameof(closePolicy));
        _appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
        _themes = theme ?? throw new ArgumentNullException(nameof(theme));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _consent = consent ?? throw new ArgumentNullException(nameof(consent));
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _runAtLogon = runAtLogon ?? throw new ArgumentNullException(nameof(runAtLogon));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
        SelectedTheme = appearance.Theme;
        SelectedLanguage = appearance.Language;
        Pending = LoadAsync();
    }

    public static string Header => Strings.Settings_Header;

    public static string AppearanceHeader => Strings.Settings_Appearance_Header;

    public static string ThemeLabel => Strings.Settings_Theme_Label;

    public static string LanguageLabel => Strings.Settings_Language_Label;

    public static string LanguageNote => Strings.Settings_Language_Note;

    public static IReadOnlyList<Choice<AppTheme>> Themes { get; } =
    [
        new(AppTheme.System, Strings.Settings_Theme_System),
        new(AppTheme.Light, Strings.Settings_Theme_Light),
        new(AppTheme.Dark, Strings.Settings_Theme_Dark),
    ];

    public static IReadOnlyList<Choice<string>> Languages { get; } =
    [
        new("en", "English"),
        new("vi", "Tiếng Việt"),
        new("ja", "日本語"),
    ];

    public static IReadOnlyList<Choice<string>> UpdateChannels { get; } =
    [
        new("stable", Strings.Settings_Channel_Stable),
        new("beta", Strings.Settings_Channel_Beta),
    ];

    public static double MinSessionMinimum => SettingsRegistry.CaptureMinSessionSeconds.Minimum;

    public static double MinSessionMaximum => SettingsRegistry.CaptureMinSessionSeconds.Maximum;

    public static double IntervalMinimum => SettingsRegistry.TelemetryIntervalMs.Minimum;

    public static double IntervalMaximum => SettingsRegistry.TelemetryIntervalMs.Maximum;

    public static double RetentionMaximum => SettingsRegistry.RetentionRawSessionsPerGame.Maximum;

    public ObservableCollection<HookedGameViewModel> HookedGames { get; } = [];

    /// <summary>
    /// D33: the games the user-mode exception concerns — in force, eligible, or held back only by the session count. The rest
    /// of the blocked games say why not on their own pages.
    /// </summary>
    public ObservableCollection<ExceptionGameViewModel> ExceptionGames { get; } = [];

    /// <summary>D33: the option is off and exceptions are kept below that do not apply.</summary>
    public bool ExceptionsSuspended => !UserModeExceptions && ExceptionGames.Any(static g => g.CanWithdraw);

    public string KillSwitchState => KillSwitch ? Strings.Settings_KillSwitch_On : Strings.Settings_KillSwitch_Off;

    /// <summary>12_BUILD: the registration exists only while a hook-enabled game references the Vulkan loader (P4 PR-2) — the button follows the same rule as the Agent's reconciler.</summary>
    public bool CanRegisterLayer => MaintenanceSnapshot.LayerStaged && !MaintenanceSnapshot.LayerRegistered && HookedGames.Any(static g => g.UsesVulkan);

    public bool CanUnregisterLayer => MaintenanceSnapshot.LayerRegistered;

    public bool CanInstallTask => MaintenanceSnapshot.Task == LogonTaskState.NotInstalled;

    public bool CanRepairTask => MaintenanceSnapshot.Task == LogonTaskState.Stale;

    public bool CanRemoveTask => MaintenanceSnapshot.Task is LogonTaskState.Installed or LogonTaskState.Stale;

    /// <summary>A pending load or write, for a test to await; the UI does not.</summary>
    public Task Pending { get; private set; } = Task.CompletedTask;

    /// <summary>Re-reads the hook-enabled list and the Agent's facts (a revoke elsewhere, a reconnect).</summary>
    public Task RefreshAsync() => Pending = RefreshCoreAsync();

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        if (_loading)
        {
            return;
        }

        _themes.Apply(value, _shell.Current);
        Pending = _appearance.SetThemeAsync(value);
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        if (_loading || string.Equals(value, _appearance.Language, StringComparison.Ordinal))
        {
            return;
        }

        Pending = ApplyLanguageAsync(value);
    }

    partial void OnKillSwitchChanged(bool value) => Persist(SettingsRegistry.HookingKillSwitch, value);

    partial void OnMinSessionSecondsChanged(int oldValue, int newValue) => PersistNumber(SettingsRegistry.CaptureMinSessionSeconds, Strings.Settings_MinSession_Label, oldValue, newValue, v => MinSessionSeconds = v);

    partial void OnTelemetryIntervalMsChanged(int oldValue, int newValue) => PersistNumber(SettingsRegistry.TelemetryIntervalMs, Strings.Settings_Interval_Label, oldValue, newValue, v => TelemetryIntervalMs = v);

    partial void OnRetentionRawSessionsChanged(int oldValue, int newValue) => PersistNumber(SettingsRegistry.RetentionRawSessionsPerGame, Strings.Settings_Retention_Label, oldValue, newValue, v => RetentionRawSessions = v);

    partial void OnBackgroundCaptureChanged(bool value) => Persist(SettingsRegistry.CaptureBackground, value);

    partial void OnHideAntiCheatHookingChanged(bool value) => Persist(SettingsRegistry.UiHideAntiCheatHooking, value);

    /// <summary>D33: the option is one settings row the Agent reads at every session start and command; the list follows it.</summary>
    partial void OnUserModeExceptionsChanged(bool value)
    {
        if (!_loading)
        {
            Pending = WriteThenRefreshAsync(SettingsRegistry.HookingUserModeExceptions, value ? "1" : "0");
        }
    }

    private async Task WriteThenRefreshAsync(SettingDefinition definition, string value)
    {
        await WriteAsync(definition, value).ConfigureAwait(true);
        await RefreshCoreAsync().ConfigureAwait(true);
    }

    partial void OnFpsTwoDecimalsChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        // Every formatter reads it from now on; a page shows it the next time it formats a number.
        FpsDecimals.Two = value;
        Persist(SettingsRegistry.UiFpsDecimals, value);
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        if (!_loading)
        {
            _closePolicy.MinimizeToTray = value;
        }

        Persist(SettingsRegistry.UiMinimizeToTray, value);
    }

    partial void OnMaintenanceSnapshotChanged(MaintenanceSnapshot value)
    {
        OnPropertyChanged(nameof(CanInstallTask));
        OnPropertyChanged(nameof(CanRepairTask));
        OnPropertyChanged(nameof(CanRemoveTask));
        TaskText = value.Task switch
        {
            LogonTaskState.Installed => Strings.Settings_Agent_Task_Installed,
            LogonTaskState.NotInstalled => Strings.Settings_Agent_Task_NotInstalled,
            LogonTaskState.Stale => Strings.Settings_Agent_Task_Stale,
            _ => Strings.Settings_Agent_Task_Unknown,
        };
        VulkanLayerText = !value.LayerStaged
            ? Strings.Settings_VkLayer_NotStaged
            : value.LayerRegistered ? Strings.Settings_VkLayer_Registered : Strings.Settings_VkLayer_NotRegistered;
    }

    /// <summary>The Agent's <c>--register-vklayer</c>, then the state as the machine holds it.</summary>
    [RelayCommand]
    private Task RegisterLayerAsync() => RunToolAsync("--register-vklayer", Strings.Settings_VkLayer_Register);

    [RelayCommand]
    private Task UnregisterLayerAsync() => RunToolAsync("--unregister-vklayer", Strings.Settings_VkLayer_Unregister);

    [RelayCommand]
    private Task InstallTaskAsync() => RunToolAsync("--install-task", Strings.Settings_Agent_Task_Register);

    /// <summary>Repair is the same flag: <c>schtasks /Create /F</c> rewrites the action with this install's path.</summary>
    [RelayCommand]
    private Task RepairTaskAsync() => RunToolAsync("--install-task", Strings.Settings_Agent_Task_Repair);

    [RelayCommand]
    private Task RemoveTaskAsync() => RunToolAsync("--uninstall-task", Strings.Settings_Agent_Task_Remove);

    partial void OnOnlineMetadataChanged(bool value) => Persist(SettingsRegistry.PrivacyOnlineMetadata, value);

    partial void OnUpdateChannelChanged(string value) => Persist(SettingsRegistry.UpdateChannel, value);

    partial void OnAutoCheckUpdatesChanged(bool value) => Persist(SettingsRegistry.UpdateAutoCheck, value);

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        try
        {
            _runAtLogon.Apply(value);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Serilog.Log.Warning(ex, "settings: the Run entry could not be written");
            _strip.Warn(Strings.Settings_StartWithWindows_Label, ex.Message);
        }

        Persist(SettingsRegistry.UiStartWithWindows, value);
    }

    partial void OnLogDebugChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        LoggingLevel.SetDebug(value);
        Persist(SettingsRegistry.LogDebug, value);
    }

    /// <summary>FR-11: the documents again, read-only — nothing is re-recorded.</summary>
    [RelayCommand]
    private Task ReopenLegalAsync() => Pending = _firstRun.ShowDocumentsAsync();

    [RelayCommand]
    private static void OpenSafetyDocs()
    {
        try
        {
            using Process? _ = Process.Start(new ProcessStartInfo(SafetyDocsUrl) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Serilog.Log.Warning(ex, "settings: could not open the safety document");
        }
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            KillSwitch = await _settings.GetBooleanAsync(SettingsRegistry.HookingKillSwitch).ConfigureAwait(true);
            MinSessionSeconds = await _settings.GetIntegerAsync(SettingsRegistry.CaptureMinSessionSeconds).ConfigureAwait(true);
            TelemetryIntervalMs = await _settings.GetIntegerAsync(SettingsRegistry.TelemetryIntervalMs).ConfigureAwait(true);
            RetentionRawSessions = await _settings.GetIntegerAsync(SettingsRegistry.RetentionRawSessionsPerGame).ConfigureAwait(true);
            BackgroundCapture = await _settings.GetBooleanAsync(SettingsRegistry.CaptureBackground).ConfigureAwait(true);
            MinimizeToTray = await _settings.GetBooleanAsync(SettingsRegistry.UiMinimizeToTray).ConfigureAwait(true);
            HideAntiCheatHooking = await _settings.GetBooleanAsync(SettingsRegistry.UiHideAntiCheatHooking).ConfigureAwait(true);
            UserModeExceptions = await _settings.GetBooleanAsync(SettingsRegistry.HookingUserModeExceptions).ConfigureAwait(true);
            FpsTwoDecimals = await _settings.GetBooleanAsync(SettingsRegistry.UiFpsDecimals).ConfigureAwait(true);
            OnlineMetadata = await _settings.GetBooleanAsync(SettingsRegistry.PrivacyOnlineMetadata).ConfigureAwait(true);
            UpdateChannel = await _settings.GetAsync(SettingsRegistry.UpdateChannel).ConfigureAwait(true);
            AutoCheckUpdates = await _settings.GetBooleanAsync(SettingsRegistry.UpdateAutoCheck).ConfigureAwait(true);
            LogDebug = await _settings.GetBooleanAsync(SettingsRegistry.LogDebug).ConfigureAwait(true);

            // The Run entry is the truth for "start with Windows"; the settings row follows it.
            StartWithWindows = _runAtLogon.IsSet;
            await RefreshCoreAsync().ConfigureAwait(true);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task RefreshCoreAsync()
    {
        IReadOnlyList<GameCard> cards = await _library.ListCardsAsync().ConfigureAwait(true);
        HookedGames.Clear();
        foreach (GameCard card in cards.Where(static c => c.Row.HookEnabled && c.Row.InLibrary).OrderBy(static c => c.Row.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            HookedGames.Add(new HookedGameViewModel(card.Row.Id, card.Row.Name, RevokeAsync, CapabilityFlags.Contains(card.Row.CapabilityFlagsJson, "vulkan")));
        }

        PresentExceptions(cards);

        Shared.Ipc.HelloAck? hello = _agent.Hello;
        ElevationText = hello is null ? Strings.Settings_VkLayer_Unknown : hello.Elevated ? Strings.Settings_Agent_Elevated : Strings.Settings_Agent_NotElevated;
        MaintenanceSnapshot = await _maintenance.ReadAsync().ConfigureAwait(true);
        OnPropertyChanged(nameof(CanRegisterLayer));
    }

    private async Task RunToolAsync(string flag, string label)
    {
        IsToolRunning = true;
        try
        {
            AgentToolResult result = await _tool.RunAsync(flag).ConfigureAwait(true);
            if (result.Succeeded)
            {
                _strip.Success(label, string.Format(CultureInfo.CurrentCulture, Strings.Settings_Tool_Done_Format, label));
            }
            else
            {
                _strip.Warn(label, string.Format(CultureInfo.CurrentCulture, Strings.Settings_Tool_Failed_Format, label, result.ExitCode, result.Output));
            }

            await RefreshCoreAsync().ConfigureAwait(true);
        }
        finally
        {
            IsToolRunning = false;
        }
    }

    /// <summary>D33: the exception list from the Agent's columns — the App reads them and never writes one.</summary>
    private void PresentExceptions(IReadOnlyList<GameCard> cards)
    {
        ExceptionGames.Clear();
        bool checking = false;
        foreach (GameCard card in cards.Where(static c => c.Row.InLibrary).OrderBy(static c => c.Row.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            if (ExceptionView.Of(card.Row) is not { } view)
            {
                continue;
            }

            checking |= view.Kind == ExceptionViewKind.Checking;
            // With the option off only a kept grant is listed (suspended): nothing may be made while it is off.
            bool listed = UserModeExceptions
                ? view.Kind is ExceptionViewKind.Granted or ExceptionViewKind.Eligible or ExceptionViewKind.TooFewSessions
                : view.Kind == ExceptionViewKind.Granted;
            if (listed)
            {
                ExceptionGames.Add(new ExceptionGameViewModel(card.Row.Id, card.Row.Name, view, GrantExceptionAsync, WithdrawExceptionAsync));
            }
        }

        ExceptionsChecking = UserModeExceptions && checking;
        OnPropertyChanged(nameof(ExceptionsSuspended));
    }

    /// <summary>D33: the disclosure, then the request; the Agent gathers the facts and grants or refuses.</summary>
    private async Task GrantExceptionAsync(ExceptionGameViewModel game)
    {
        AntiCheatExceptionResult result = _exceptions is null
            ? AntiCheatExceptionResult.Of(AntiCheatExceptionOutcome.AgentUnavailable)
            : await _exceptions.GrantAsync(game.GameId, new AntiCheatExceptionFacts(game.Name, game.View.Family ?? string.Empty,
                game.View.Signal ?? string.Empty, game.View.Sessions)).ConfigureAwait(true);
        await ReportExceptionAsync(game.Name, result).ConfigureAwait(true);
    }

    private async Task WithdrawExceptionAsync(ExceptionGameViewModel game)
    {
        AntiCheatExceptionResult result = _exceptions is null
            ? AntiCheatExceptionResult.Of(AntiCheatExceptionOutcome.AgentUnavailable)
            : await _exceptions.WithdrawAsync(game.GameId).ConfigureAwait(true);
        await ReportExceptionAsync(game.Name, result).ConfigureAwait(true);
    }

    private async Task ReportExceptionAsync(string name, AntiCheatExceptionResult result)
    {
        if (result.MessageFor(name) is { } message)
        {
            if (result.Succeeded)
            {
                _strip.Success(name, message);
            }
            else
            {
                _strip.Warn(name, message);
            }
        }

        await RefreshCoreAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private Task RefreshExceptionsAsync() => RefreshAsync();

    private async Task RevokeAsync(HookedGameViewModel game)
    {
        HookingConsentResult result = await _consent.DisableAsync(game.GameId).ConfigureAwait(true);
        if (result.Outcome == HookingConsentOutcome.Disabled)
        {
            HookedGames.Remove(game);
            return;
        }

        string body = result.Outcome == HookingConsentOutcome.AgentUnavailable ? Shared.Strings.Safety_Consent_AgentUnavailable : result.Detail ?? Strings.Common_NotAvailable;
        _strip.Warn(game.Name, body);
    }

    private void Persist(SettingDefinition definition, bool value)
    {
        if (!_loading)
        {
            Pending = WriteAsync(definition, value ? "1" : "0");
        }
    }

    private void Persist(SettingDefinition definition, string value)
    {
        if (!_loading)
        {
            Pending = WriteAsync(definition, value);
        }
    }

    /// <summary>A number outside the registry's range is refused in place — the previous value comes back and nothing is written.</summary>
    private void PersistNumber(SettingDefinition definition, string label, int previous, int value, Action<int> revert)
    {
        if (_loading)
        {
            return;
        }

        string text = value.ToString(CultureInfo.InvariantCulture);
        if (definition.Validate(text) is not null)
        {
            _strip.Warn(label, string.Format(CultureInfo.CurrentCulture, Strings.Settings_Invalid_Format, value, definition.Minimum, definition.Maximum));
            _loading = true;
            try
            {
                revert(previous);
            }
            finally
            {
                _loading = false;
            }

            return;
        }

        Pending = WriteAsync(definition, text);
    }

    private async Task WriteAsync(SettingDefinition definition, string value)
    {
        await _settings.SetAsync(definition, value).ConfigureAwait(true);
        if (definition.AgentReads)
        {
            _strip.Info(Strings.Settings_Header, Strings.Settings_Applied);
        }
    }

    private async Task ApplyLanguageAsync(string value)
    {
        await _appearance.SetLanguageAsync(value).ConfigureAwait(true);
        App.ApplyCulture(_appearance.Language);
        _shell.Rebuild();
    }
}
