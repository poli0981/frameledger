using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Charts;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Settings;
using FrameLedger.Application.TriState;
using FrameLedger.Domain.Detection;
using FrameLedger.Domain.Sessions;
using FrameLedger.Infrastructure.Io;
using Wpf.Ui.Controls;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// The game page (<c>08_UI</c> §Games › Game detail): header, the two clearly separated rows (Supports vs Last
/// session measured), the hooking control over <see cref="HookingConsent"/> (FR-2.1/2.2/2.5), the Sessions tab.
/// A refusal or a mismatch is a persistent notice on this page, never a toast (§Notifications policy).
/// </summary>
[SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed partial class GameDetailViewModel : ObservableObject
{
    private readonly GameLibrary _library;
    private readonly HookingConsent _consent;
    private readonly IPageNavigator _navigator;
    private readonly IConfirmations _confirmations;
    private readonly IEditGamePrompt _edit;
    private readonly IGamePicker _picker;
    private readonly IMessageStrip _strip;
    private readonly ISessionSummaryOpener _summaries;
    private readonly SessionSeriesLoader _loader;
    private readonly IHardwareSnapshotRepository _hardware;
    private readonly SessionSelection _selection;
    private readonly RegisteredSettings? _settings;
    private readonly long? _gameId;

    // ui.hide_anticheat_hooking (2026-09-25), read at each load; on until the settings say otherwise, as its default is.
    private bool _hideAntiCheatHooking = true;
    private GameDetail? _detail;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _subtitle = string.Empty;

    /// <summary>The executable the row is about: what the watcher matches and what consent is keyed on, so a wrong import guess is visible.</summary>
    [ObservableProperty]
    private string _executableText = string.Empty;

    /// <summary>
    /// The row's executable is not where the row says (2026-09-22). Read when the page loads; the Agent moves the row
    /// itself when the file is under another drive letter, so this clears on the next visit once it has.
    /// </summary>
    [ObservableProperty]
    private bool _executableMissing;

    [ObservableProperty]
    private string _lastSessionText = string.Empty;

    [ObservableProperty]
    private FpsReadoutModel _lifetime = FpsReadoutModel.Unavailable;

    [ObservableProperty]
    private bool _supportsEmpty = true;

    [ObservableProperty]
    private string _measuredEmptyText = string.Empty;

    /// <summary>The entry's recording switch (schema 0009, 2026-09-23): off, FrameLedger does not watch for the program.</summary>
    [ObservableProperty]
    private bool _recordSessions = true;

    [ObservableProperty]
    private string _recordingStatusText = string.Empty;

    [ObservableProperty]
    private bool _hookEnabled;

    [ObservableProperty]
    private bool _hookToggleEnabled;

    [ObservableProperty]
    private string _hookStatusText = string.Empty;

    [ObservableProperty]
    private string? _blockedText;

    /// <summary>
    /// The compact finding shown IN PLACE of the Hooking card when anti-cheat was found and the user keeps such cards hidden
    /// (beta.8); null otherwise. The finding itself is always on the page (FR-2.2) — here, or as the card's blocked text.
    /// </summary>
    [ObservableProperty]
    private string? _antiCheatText;

    /// <summary>Whether the Hooking card and its lines show: false only for an anti-cheat game while they are hidden.</summary>
    [ObservableProperty]
    private bool _hookingSectionVisible = true;

    /// <summary>
    /// Why the switch cannot be turned on when the executable cannot run as x64 (beta.8, <c>exe_machine</c>); null otherwise.
    /// A switch that is on for such a game can still be turned off.
    /// </summary>
    [ObservableProperty]
    private string? _notX64Text;

    [ObservableProperty]
    private string? _autoDisabledText;

    [ObservableProperty]
    private string? _unverifiedText;

    [ObservableProperty]
    private string? _noticeText;

    [ObservableProperty]
    private InfoBarSeverity _noticeSeverity = InfoBarSeverity.Warning;

    [ObservableProperty]
    private bool _busy;

    [ObservableProperty]
    private bool _notFound;

    [ObservableProperty]
    private bool _sessionsEmpty = true;

    [ObservableProperty]
    private SessionItemViewModel? _selectedSession;

    [ObservableProperty]
    private string _selectedNote = Strings.Tabs_SelectASession;

    [ObservableProperty]
    private bool _selectedHasSeries;

    [ObservableProperty]
    private string _latencyText = string.Empty;

    [ObservableProperty]
    private bool _hasLatency;

    [ObservableProperty]
    private TrendMetric _trendMetric = TrendMetric.PresentedFps;

    [ObservableProperty]
    private bool _includeMidSession;

    [ObservableProperty]
    private bool _trendEmpty = true;

    [ObservableProperty]
    private string _trendExcludedText = string.Empty;

    public GameDetailViewModel(GameLibrary library, GameSelection selection, HookingConsent consent, IPageNavigator navigator,
        IConfirmations confirmations, IEditGamePrompt edit, IMessageStrip strip, ISessionSummaryOpener summaries,
        SessionSeriesLoader loader, IHardwareSnapshotRepository hardware, SessionSelection sessionSelection, IGamePicker picker,
        RegisteredSettings? settings = null)
    {
        _settings = settings;
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        _selection = sessionSelection ?? throw new ArgumentNullException(nameof(sessionSelection));
        _library = library ?? throw new ArgumentNullException(nameof(library));
        ArgumentNullException.ThrowIfNull(selection);
        _consent = consent ?? throw new ArgumentNullException(nameof(consent));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _confirmations = confirmations ?? throw new ArgumentNullException(nameof(confirmations));
        _edit = edit ?? throw new ArgumentNullException(nameof(edit));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
        _summaries = summaries ?? throw new ArgumentNullException(nameof(summaries));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
        _gameId = selection.GameId;
        Pending = LoadAsync();
    }

    public static string BackText => Strings.GameDetail_Back;

    public static string EditText => Strings.GameDetail_Edit;

    public static string RemoveText => Strings.GameDetail_Remove;

    public static string ChangeExecutableText => Strings.GameDetail_ChangeExe;

    public static string ExecutableMissingText => Strings.GameDetail_ExeMissing;

    /// <summary>
    /// The missing executable's bar (beta.8): the page's sentence, then whether the DRIVE is not connected or the file is gone
    /// from its path — the two need different things of the user.
    /// </summary>
    public string ExecutableMissingMessage => Game is null
        ? ExecutableMissingText
        : ExecutableMissingText + " " + Formats.ExecutableMissingText(Game.Fingerprint.ExePath);

    public static string LifetimeLabel => Strings.GameDetail_LifetimeAvg;

    public static string SupportsHeader => Strings.GameDetail_Supports_Header;

    public static string SupportsEmptyText => Strings.GameDetail_Supports_Empty;

    public static string MeasuredHeader => Strings.GameDetail_Measured_Header;

    public static string RecordingHeader => Strings.GameDetail_Recording_Header;

    public static string RecordingBody => Strings.GameDetail_Recording_Body;

    public static string HookingHeader => Strings.GameDetail_Hooking_Header;

    public static string AntiCheatHeader => Strings.GameDetail_AntiCheat_Header;

    public static string DetailsHeader => Strings.GameDetail_Details_Header;

    /// <summary>What the game's files and its store say about it (beta.8): what it runs as, its versions, what it ships.</summary>
    public ObservableCollection<GameDetailRow> Details { get; } = [];

    public static string HookingBody => Strings.GameDetail_Hooking_Body;

    public static string ReEnableText => Strings.GameDetail_Hooking_ReEnable;

    public static string BusyText => Strings.GameDetail_Hooking_Busy;

    public static string SessionsEmptyText => Strings.GameDetail_Sessions_Empty;

    public static string NotFoundText => Strings.GameDetail_NotFound;

    public static string TabSessions => Strings.GameDetail_Tab_Sessions;

    public static string TabFrametime => Strings.GameDetail_Tab_Frametime;

    public static string TabDistribution => Strings.GameDetail_Tab_Distribution;

    public static string TabTrend => Strings.GameDetail_Tab_Trend;

    public static string TabSensors => Strings.GameDetail_Tab_Sensors;

    public static string TabLatency => Strings.GameDetail_Tab_Latency;

    public static string TrendMetricLabel => Strings.Trend_Metric_Label;

    public static string IncludeMidSessionText => Strings.Trend_IncludeMidSession;

    public static string TrendEmptyText => Strings.Trend_Empty;

    public static string TrendAverageNote => Strings.Trend_Average_Note;

    public static string TrendPartialNote => Strings.Trend_Partial_Note;

    /// <summary>Why the trend is empty FOR THIS METRIC (beta.8): "no hooked session yet" was wrong for a game whose sessions simply lack it.</summary>
    [ObservableProperty]
    private string _trendEmptyMessage = Strings.Trend_Empty;

    public static string LatencyHeader => Strings.Latency_Header;

    public static string LatencyEmptyText => Strings.Latency_Empty;

    public static string SensorsEmptyText => Strings.Sensors_Empty;

    /// <summary>
    /// The selector's entries, built for each page in the language it opens in (beta.8: a static list kept the language the
    /// App started in).
    /// </summary>
    public IReadOnlyList<Choice<TrendMetric>> TrendMetrics { get; } =
    [
        new(TrendMetric.PresentedFps, Strings.Trend_Metric_PresentedFps),
        new(TrendMetric.NativeFps, Strings.Trend_Metric_NativeFps),
        new(TrendMetric.Displayed, Strings.Trend_Metric_Displayed),
        new(TrendMetric.P1Low, Strings.Trend_Metric_P1Low),
        new(TrendMetric.P01Low, Strings.Trend_Metric_P01Low),
        new(TrendMetric.MaxGpuTemp, Strings.Trend_Metric_MaxGpuTemp),
        new(TrendMetric.AvgGpuLoad, Strings.Trend_Metric_AvgGpuLoad),
        new(TrendMetric.AvgGpuPower, Strings.Trend_Metric_AvgGpuPower),
        new(TrendMetric.MaxVramProcess, Strings.Trend_Metric_MaxVramProcess),
        new(TrendMetric.AvgCpuLoad, Strings.Trend_Metric_AvgCpuLoad),
        new(TrendMetric.MaxCpuTemp, Strings.Trend_Metric_MaxCpuTemp),
        new(TrendMetric.AvgRam, Strings.Trend_Metric_AvgRam),
        new(TrendMetric.FgFactor, Strings.Trend_Metric_FgFactor),
    ];

    /// <summary>The selected session's decoded series (null: none selected, Tier 2, or swept).</summary>
    public SessionSeries? SelectedSeries { get; private set; }

    public IReadOnlyList<TrendPoint> TrendPoints { get; private set; } = [];

    public IReadOnlyList<HardwareChange> HardwareChanges { get; private set; } = [];

    public string TrendMetricText => TrendMetrics.First(c => c.Value == TrendMetric).Label;

    /// <summary>Raised when the selected session's series (re)loaded — the Frametime, Distribution, Sensors and Latency tabs redraw.</summary>
    public event EventHandler? SelectionPresented;

    /// <summary>Raised when the trend was rebuilt.</summary>
    public event EventHandler? TrendPresented;

    public GameRow? Game { get; private set; }

    public ObservableCollection<TriStateChipModel> Chips { get; } = [];

    public ObservableCollection<string> Supports { get; } = [];

    public ObservableCollection<string> Measured { get; } = [];

    public ObservableCollection<SessionItemViewModel> Sessions { get; } = [];

    public bool NoticeVisible => NoticeText is not null;

    /// <summary>The load or the action in flight, for a test to await.</summary>
    public Task Pending { get; private set; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        GameDetail? detail = _gameId is long id ? await _library.LoadAsync(id, ct: ct).ConfigureAwait(true) : null;
        if (detail is null)
        {
            NotFound = true;
            Game = null;
            return;
        }

        Game = detail.Row;
        _detail = detail;
        NotFound = false;
        if (_settings is not null)
        {
            _hideAntiCheatHooking = await _settings.GetBooleanAsync(SettingsRegistry.UiHideAntiCheatHooking, ct).ConfigureAwait(true);
        }

        Present(detail);
        ChooseDefaultMetric(detail);
        await RebuildTrendAsync(ct).ConfigureAwait(true);
    }

    partial void OnSelectedSessionChanged(SessionItemViewModel? value)
    {
        _selection.Set(value?.Id);    // File ▸ Export acts on this (P4 PR-3)
        Pending = LoadSelectedAsync(value);
    }

    partial void OnTrendMetricChanged(TrendMetric value)
    {
        _trendMetricChosen |= !_choosingDefaultMetric;
        RebuildTrendPoints();
    }

    private bool _trendMetricChosen;

    private bool _choosingDefaultMetric;

    /// <summary>
    /// The first metric a game's trend opens on (beta.8): Presented FPS, unless the game's newest measured session counted
    /// generated frames — then Native FPS, the rate such sessions have. Kept once the user picks one.
    /// </summary>
    private void ChooseDefaultMetric(GameDetail detail)
    {
        if (_trendMetricChosen)
        {
            return;
        }

        SessionRow? newest = detail.Sessions.FirstOrDefault(static s => s.Tier == CaptureTier.Hooked && s.FrameCount > 0);
        _choosingDefaultMetric = true;
        TrendMetric = newest is not null && FpsPresentation.FromRow(newest).Kind == FpsReadoutKind.Generated ? TrendMetric.NativeFps : TrendMetric.PresentedFps;
        _choosingDefaultMetric = false;
    }

    partial void OnIncludeMidSessionChanged(bool value) => RebuildTrendPoints();

    private async Task LoadSelectedAsync(SessionItemViewModel? session)
    {
        SelectedSeries = null;
        HasLatency = false;
        LatencyText = string.Empty;
        if (session is null)
        {
            SelectedNote = Strings.Tabs_SelectASession;
        }
        else if (!session.IsHooked)
        {
            SelectedNote = Strings.Tabs_SelectedNotHooked;
        }
        else
        {
            SelectedSeries = await _loader.LoadAsync(session.Id).ConfigureAwait(true);
            SelectedNote = SelectedSeries is null ? Strings.Tabs_SelectedNoFrames : string.Empty;
            HasLatency = SelectedSeries?.LatencyUs is { Length: > 0 } && session.Row.ReflexActive == true;
            LatencyText = HasLatency && session.Row.LatencyAvgUs is long avg && session.Row.LatencyP95Us is long p95
                ? string.Format(CultureInfo.CurrentCulture, Strings.Latency_Stats_Format, avg / 1000.0, p95 / 1000.0)
                : string.Empty;
        }

        SelectedHasSeries = SelectedSeries is not null;
        SelectionPresented?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>FR-6.3: the snapshots of this game's sessions, then the markers between consecutive ones that differ.</summary>
    private async Task RebuildTrendAsync(CancellationToken ct)
    {
        if (_detail is null)
        {
            return;
        }

        var snapshots = new Dictionary<long, HardwareSnapshot?>();
        foreach (long id in _detail.Sessions.Select(static s => s.SnapshotId).Distinct())
        {
            snapshots[id] = await _hardware.FindAsync(id, ct).ConfigureAwait(true);
        }

        HardwareChanges = TrendSeriesBuilder.Changes(_detail.Sessions, id => snapshots.GetValueOrDefault(id));
        RebuildTrendPoints();
    }

    private void RebuildTrendPoints()
    {
        if (_detail is null)
        {
            return;
        }

        TrendPoints = TrendSeriesBuilder.Points(_detail.Sessions, TrendMetric, IncludeMidSession);
        int excluded = IncludeMidSession ? 0 : TrendSeriesBuilder.ExcludedCount(_detail.Sessions, TrendMetric);
        TrendExcludedText = excluded > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.Trend_Excluded_Format, excluded) : string.Empty;
        TrendEmpty = TrendPoints.Count == 0;
        TrendEmptyMessage = string.Format(CultureInfo.CurrentCulture, Strings.Trend_Empty_Format, TrendMetricText);
        OnPropertyChanged(nameof(TrendMetricText));
        TrendPresented?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Back() => _navigator.Navigate<GamesPage>();

    /// <summary>A session row opens its summary window (08_UI §Games: "Row → Session summary").</summary>
    [RelayCommand]
    private void OpenSession(SessionItemViewModel? session)
    {
        if (session is not null)
        {
            _summaries.Open(session.Id);
        }
    }

    [RelayCommand]
    private Task ToggleHookingAsync() => RunAsync(HookEnabled ? DisableAsync : EnableAsync);

    [RelayCommand]
    private Task ToggleRecordingAsync() => RunAsync(ToggleRecordingCoreAsync);

    [RelayCommand]
    private Task ReEnableHookingAsync() => RunAsync(EnableAsync);

    [RelayCommand]
    private Task EditAsync() => RunAsync(EditCoreAsync);

    [RelayCommand]
    private Task RemoveAsync() => RunAsync(RemoveCoreAsync);

    [RelayCommand]
    private Task ChangeExecutableAsync() => RunAsync(ChangeExecutableCoreAsync);

    /// <summary>One action at a time, and the one in flight is <see cref="Pending"/> for a test to await.</summary>
    private async Task RunAsync(Func<Task> work)
    {
        Task running = work();
        Pending = running;
        await running.ConfigureAwait(true);
    }

    private async Task EnableAsync()
    {
        if (Game is null)
        {
            return;
        }

        Busy = true;
        try
        {
            HookingConsentResult result = await _consent.EnableAsync(Game.Id, Game.Name).ConfigureAwait(true);
            Notice(result);
        }
        finally
        {
            Busy = false;
        }

        await LoadAsync().ConfigureAwait(true);
    }

    private async Task DisableAsync()
    {
        if (Game is null)
        {
            return;
        }

        Busy = true;
        try
        {
            HookingConsentResult result = await _consent.DisableAsync(Game.Id).ConfigureAwait(true);
            Notice(result);
        }
        finally
        {
            Busy = false;
        }

        await LoadAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Flips the entry's recording switch (2026-09-23). No confirmation: it deletes nothing — sessions already recorded stay,
    /// and turning it back on is the same click.
    /// </summary>
    private async Task ToggleRecordingCoreAsync()
    {
        if (Game is null)
        {
            return;
        }

        await _library.SetRecordingAsync(Game.Id, !Game.RecordSessions).ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task EditCoreAsync()
    {
        if (Game is null)
        {
            return;
        }

        GameMetadata? edited = await _edit.EditAsync(GameMetadata.Of(Game), Game.FieldProvenanceJson).ConfigureAwait(true);
        if (edited is null)
        {
            return;
        }

        await _library.UpdateMetadataAsync(Game.Id, edited).ConfigureAwait(true);
        _strip.Success(Strings.EditGame_Title, Strings.EditGame_Saved);
        await LoadAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// A store import guesses the executable (Steam names none), and everything downstream is keyed on it: a wrong guess
    /// is a game that can never be matched. The pick is the user's own; the row comes back hooking-off and unconsented,
    /// exactly as a new game would, and consent is revoked over the pipe first, as removal does.
    /// </summary>
    private async Task ChangeExecutableCoreAsync()
    {
        if (Game is null)
        {
            return;
        }

        string? path = _picker.PickExecutable();
        if (path is null || string.Equals(ExecutableIdentity.Normalise(path), Game.Fingerprint.ExePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Game.HookEnabled)
        {
            await _consent.DisableAsync(Game.Id).ConfigureAwait(true);
        }

        bool? changed = await _library.ChangeExecutableAsync(Game.Id, path).ConfigureAwait(true);
        string file = Path.GetFileName(path);
        switch (changed)
        {
            case null:
                _strip.Warn(Strings.GameDetail_ChangeExe, string.Format(CultureInfo.CurrentCulture, Strings.ChangeExe_Unreadable_Format, file));
                break;
            case false:
                _strip.Warn(Strings.GameDetail_ChangeExe, string.Format(CultureInfo.CurrentCulture, Strings.ChangeExe_Taken_Format, file));
                break;
            default:
                _strip.Success(Strings.GameDetail_ChangeExe, string.Format(CultureInfo.CurrentCulture, Strings.ChangeExe_Done_Format, Game.Name, file));
                break;
        }

        await LoadAsync().ConfigureAwait(true);
    }

    private async Task RemoveCoreAsync()
    {
        if (Game is null)
        {
            return;
        }

        RemoveGameChoice choice = await _confirmations.RemoveGameAsync(Game.Name).ConfigureAwait(true);
        if (choice == RemoveGameChoice.Cancel)
        {
            return;
        }

        // Consent is revoked over the pipe first (never through the table): the row may stay, hooking must not.
        if (Game.HookEnabled)
        {
            await _consent.DisableAsync(Game.Id).ConfigureAwait(true);
        }

        string name = Game.Name;
        await _library.RemoveAsync(Game.Id, keepSessions: choice == RemoveGameChoice.KeepSessions).ConfigureAwait(true);
        _strip.Info(Strings.Games_Header, string.Format(CultureInfo.CurrentCulture, Strings.RemoveGame_Removed_Format, name));
        _navigator.Navigate<GamesPage>();
    }

    /// <summary>A refusal, a mismatch or a failure is a persistent notice on the page; a success clears it.</summary>
    private void Notice(HookingConsentResult result)
    {
        (NoticeText, NoticeSeverity) = result.Outcome switch
        {
            HookingConsentOutcome.Refused => (result.RefusalText(), InfoBarSeverity.Error),
            HookingConsentOutcome.VersionMismatch => (Shared.Strings.Safety_Consent_VersionMismatch, InfoBarSeverity.Warning),
            HookingConsentOutcome.AgentUnavailable => (Shared.Strings.Safety_Consent_AgentUnavailable, InfoBarSeverity.Warning),
            // The Agent could not read the executable (beta.8): say whether its drive or the file is missing, not the IPC code.
            HookingConsentOutcome.Failed when string.Equals(result.Detail, Shared.Ipc.IpcErrorCode.ExecutableUnreadable, StringComparison.Ordinal) && Game is not null
                => (Formats.ExecutableMissingText(Game.Fingerprint.ExePath), InfoBarSeverity.Warning),
            HookingConsentOutcome.Failed => (result.Detail ?? Strings.Common_NotAvailable, InfoBarSeverity.Warning),
            _ => (null, InfoBarSeverity.Informational),
        };
        OnPropertyChanged(nameof(NoticeVisible));
    }

    private void Present(GameDetail detail)
    {
        GameRow row = detail.Row;
        Name = row.Name;
        // A store's version is said as one ("Steam build 20374416", beta.8), and then names the store itself.
        bool storeVersion = EditGameViewModel.IsDetectedField(row.FieldProvenanceJson, "game_version") && row.Platform is "steam" or "gog" or "epic";
        string?[] subtitle = storeVersion
            ? [row.Publisher, Formats.StoreVersion(row.Platform, row.GameVersion, detected: true) ?? Formats.Platform(row.Platform), EngineText(row)]
            : [row.Publisher, row.GameVersion, Formats.Platform(row.Platform), EngineText(row)];
        Subtitle = string.Join(" · ", subtitle.Where(static s => !string.IsNullOrWhiteSpace(s)));
        ExecutableText = row.Fingerprint.ExePath;
        ExecutableMissing = !GameLibrary.ExecutableExists(row.Fingerprint.ExePath);
        OnPropertyChanged(nameof(ExecutableMissingMessage));

        SessionRow? last = detail.Sessions.Count > 0 ? detail.Sessions[0] : null;
        SessionRow? lastHooked = detail.Sessions.FirstOrDefault(static s => s.Tier == CaptureTier.Hooked && s.FrameCount > 0);
        LastSessionText = last is null ? string.Empty : string.Format(CultureInfo.CurrentCulture, Strings.GameDetail_LastSession_Format, Formats.Date(last.StartedAt), Formats.Api(last.Api));
        Lifetime = lastHooked is null ? FpsReadoutModel.Unavailable : FpsPresentation.FromRow(lastHooked);

        PresentChips(detail, lastHooked);
        PresentSupports(row);
        PresentDetails(detail);
        PresentMeasured(last, lastHooked);
        PresentRecording(row);
        PresentHooking(row);
        PresentSessions(detail);
    }

    private void PresentRecording(GameRow row)
    {
        RecordSessions = row.RecordSessions;
        RecordingStatusText = row.RecordSessions ? Strings.GameDetail_Recording_On : Strings.GameDetail_Recording_Off;
    }

    private void PresentChips(GameDetail detail, SessionRow? lastHooked)
    {
        Chips.Clear();
        foreach (TriStateKind kind in new[] { TriStateKind.RayTracing, TriStateKind.PathTracing, TriStateKind.RayReconstruction })
        {
            ResolvedTriState resolved = lastHooked is null
                ? TriStateResolution.Resolve(null, Domain.Metrics.Tri.NotApplicable, DefaultOf(detail.Row, kind))
                : TriStateResolution.Resolve(kind, lastHooked, detail.Annotations.GetValueOrDefault(lastHooked.Id), detail.Row);
            Chips.Add(TriStateChipModel.Of(kind, resolved));
        }
    }

    private void PresentSupports(GameRow row)
    {
        Supports.Clear();
        foreach (string capability in CapabilityNames(row.CapabilityFlagsJson))
        {
            Supports.Add(string.Format(CultureInfo.CurrentCulture, Strings.GameDetail_Supports_Format, capability));
        }

        SupportsEmpty = Supports.Count == 0;
    }

    private void PresentDetails(GameDetail detail)
    {
        GameRow row = detail.Row;
        Details.Clear();
        if (row.ExeMachine is null)
        {
            // Written by the Agent's detection sweep: a row it has not read under this build yet.
            Details.Add(new GameDetailRow(Strings.GameDetail_Details_Architecture, Strings.GameDetail_Details_NotRead));
        }
        else
        {
            Details.Add(new GameDetailRow(Strings.GameDetail_Details_Architecture, Formats.Architecture(row.ExeMachine)));
            AddDetail(Strings.GameDetail_Details_FileVersion, row.ExeFileVersion);
            AddDetail(Strings.GameDetail_Details_ProductVersion, row.ExeProductVersion);
        }

        bool detected = EditGameViewModel.IsDetectedField(row.FieldProvenanceJson, "game_version");
        AddDetail(detected ? Strings.GameDetail_Details_Store : Strings.GameDetail_Details_Version, Formats.StoreVersion(row.Platform, row.GameVersion, detected));
        AddDetail(Strings.GameDetail_Details_Engine, EngineText(row));
        if (row.ExeMachine is not null)
        {
            Details.Add(new GameDetailRow(Strings.GameDetail_Details_Libraries, row.Libraries.Count == 0
                ? Strings.GameDetail_Details_NoLibraries
                : string.Join(Environment.NewLine, row.Libraries.Select(LibraryText))));
        }

        // beta.8: the NVIDIA App's overrides, as the driver profile stood at the newest session that read one (sessions
        // come newest first). Nothing is shown where no session could ask — a machine without an NVIDIA driver.
        if (detail.Sessions.Select(static s => Application.Recording.DriverProfileRecord.Parse(s.DriverProfileJson))
                .FirstOrDefault(static p => p is not null && NvidiaOverrides.IsRead(p)) is { } profile)
        {
            Details.Add(new GameDetailRow(Strings.GameDetail_Details_NvidiaOverride, NvidiaOverrides.OverridesText(profile)));
        }
    }

    private void AddDetail(string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            Details.Add(new GameDetailRow(label, value));
        }
    }

    /// <summary>"DLSS: nvngx_dlss.dll 3.7.10.0" — the product the rule names, the file, and what the file says it is.</summary>
    private static string LibraryText(LibraryFile library)
    {
        string product = CapabilityName(library.CapabilityId) is { Length: > 0 } name ? name : library.CapabilityId;
        return library.FileVersion is { } version
            ? string.Format(CultureInfo.CurrentCulture, Strings.GameDetail_Details_Library_Format, product, library.FileName, version)
            : string.Format(CultureInfo.CurrentCulture, Strings.GameDetail_Details_LibraryNoVersion_Format, product, library.FileName);
    }

    private void PresentMeasured(SessionRow? last, SessionRow? lastHooked)
    {
        Measured.Clear();
        if (lastHooked is null)
        {
            MeasuredEmptyText = last is null ? Strings.GameDetail_Measured_Empty : Strings.GameDetail_Measured_NotHooked;
            return;
        }

        MeasuredEmptyText = string.Empty;
        string upscaler = Formats.Upscaler(lastHooked.Upscaler, lastHooked.UpscalerQuality, lastHooked.UpscalerDriverReported);
        Measured.Add(upscaler + " · " + Formats.Resolution(lastHooked.RenderW, lastHooked.RenderH, lastHooked.OutputW, lastHooked.OutputH));
        Measured.Add(FpsPresentation.FrameGenerationLabel(FpsPresentation.FromRow(lastHooked), lastHooked.FgMode));
        foreach (TriStateChipModel chip in Chips.Where(static c => !c.IsNotApplicable))
        {
            Measured.Add(chip.Text);
        }
    }

    private void PresentHooking(GameRow row)
    {
        HookEnabled = row.HookEnabled;
        bool blocked = row.BlockedByGuard;

        // A blocked row's toggle is disabled because enabling it could only be refused (19_SAFETY §What a finding does
        // to the game): the finding is on the page, and there is no switch that overrules it (2026-09-22).
        // An executable that cannot run as x64 (beta.8): turning hooking ON could only be refused; a switch that is on
        // (enabled before this build read the file) can still be turned off.
        bool notX64 = ExecutableArchitecture.IsKnownNotHookable(row.ExeMachine);
        HookToggleEnabled = !blocked && !Busy && (row.HookEnabled || !notX64);
        NotX64Text = notX64 && !blocked
            ? string.Format(CultureInfo.CurrentCulture, Strings.Hooking_NotX64_Format, Formats.Architecture(row.ExeMachine))
            : null;
        HookStatusText = row.HookEnabled ? Strings.GameDetail_Hooking_On : Strings.GameDetail_Hooking_Off;
        string? found = blocked ? BlockedReasonText.Describe(row.HookBlockedReason ?? row.HookPrescanState) : null;
        BlockedText = found is null
            ? null
            : string.Format(CultureInfo.CurrentCulture, Shared.Strings.Safety_Blocked_Toggle_Format, found);

        // beta.8 (owner request 2026-09-25): for an anti-cheat game the card is a switch that can never be turned on, so
        // by default it gives way to the finding alone. The setting off keeps the card, with the finding under it.
        HookingSectionVisible = !(blocked && _hideAntiCheatHooking);
        AntiCheatText = found is not null && !HookingSectionVisible
            ? string.Format(CultureInfo.CurrentCulture, Strings.GameDetail_AntiCheat_Format, found)
            : null;
        // Its "Turn hooking back on" could only be refused once anti-cheat was found, or for an executable that cannot run
        // as x64, so neither row offers it.
        AutoDisabledText = !blocked && !notX64 && row.HookAutoDisabledReason is { Length: > 0 } reason && !row.HookEnabled
            ? string.Format(CultureInfo.CurrentCulture, Shared.Strings.Safety_AutoDisabled_Format, reason)
            : null;
        UnverifiedText = string.Equals(row.HookPrescanState, "unverified", StringComparison.Ordinal) ? Strings.GameDetail_Hooking_Unverified : null;
    }

    private void PresentSessions(GameDetail detail)
    {
        Sessions.Clear();
        foreach (SessionRow row in detail.Sessions)
        {
            Sessions.Add(new SessionItemViewModel(row, detail.Annotations.GetValueOrDefault(row.Id), detail.Row));
        }

        SessionsEmpty = Sessions.Count == 0;
    }

    private static string EngineText(GameRow row) =>
        row.Engine is null || row.EngineVersion is null ? Formats.Engine(row.Engine) : Formats.Engine(row.Engine) + " " + row.EngineVersion;

    private static Domain.Metrics.Tri DefaultOf(GameRow row, TriStateKind kind) => kind switch
    {
        TriStateKind.RayTracing => row.RtDefault,
        TriStateKind.PathTracing => row.PtDefault,
        _ => row.RrDefault,
    };

    /// <summary>
    /// <c>capability_flags</c> (<c>05_DETECTION</c> §Capability hints): an array of the rule ids the Agent's detection sweep
    /// writes (P4 PR-1 — <c>dlss</c>, <c>dlss_g</c>, <c>dlss_rr</c>, <c>streamline</c>, <c>fsr</c>, <c>xess</c>, <c>xefg</c>), or
    /// an object of flags in the older spelling, → product names. An unreadable value is no chips.
    /// </summary>
    internal static IReadOnlyList<string> CapabilityNames(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            IEnumerable<string> tokens = json.TrimStart().StartsWith('[')
                ? JsonSerializer.Deserialize(json, AppJsonContext.Default.StringArray) ?? []
                : (JsonSerializer.Deserialize(json, AppJsonContext.Default.DictionaryStringBoolean) ?? []).Where(static kv => kv.Value).Select(static kv => kv.Key);
            return [.. tokens.Select(CapabilityName).Where(static n => n.Length > 0)];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    internal static string CapabilityName(string token) => token switch
    {
        "dlss" => "DLSS",
        "dlssg" or "dlss_g" => "DLSS-G",
        "dlssd" or "dlss_rr" => "DLSS Ray Reconstruction",
        "streamline" => "Streamline",
        "vulkan" => "Vulkan",
        "fsr" => "FSR",
        "fsrfg" => "FSR Frame Generation",
        "xess" => "XeSS",
        "xefg" => "XeFG",
        "reflex" => "Reflex",
        _ => string.Empty,
    };
}
