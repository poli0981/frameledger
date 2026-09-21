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
using FrameLedger.Application.TriState;
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
    private readonly GuardBypass _bypass;
    private readonly IMessageStrip _strip;
    private readonly ISessionSummaryOpener _summaries;
    private readonly SessionSeriesLoader _loader;
    private readonly IHardwareSnapshotRepository _hardware;
    private readonly SessionSelection _selection;
    private readonly long? _gameId;
    private GameDetail? _detail;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _subtitle = string.Empty;

    /// <summary>
    /// The per-game guard bypass as the row states it (owner decision 2026-09-21). <c>OneWay</c> on the page: the switch
    /// moves only when the Agent has recorded or withdrawn the acknowledgement and the row was re-read.
    /// </summary>
    [ObservableProperty]
    private bool _guardBypassOn;

    /// <summary>The executable the row is about: what the watcher matches and what consent is keyed on, so a wrong import guess is visible.</summary>
    [ObservableProperty]
    private string _executableText = string.Empty;

    [ObservableProperty]
    private string _lastSessionText = string.Empty;

    [ObservableProperty]
    private FpsReadoutModel _lifetime = FpsReadoutModel.Unavailable;

    [ObservableProperty]
    private bool _supportsEmpty = true;

    [ObservableProperty]
    private string _measuredEmptyText = string.Empty;

    [ObservableProperty]
    private bool _hookEnabled;

    [ObservableProperty]
    private bool _hookToggleEnabled;

    [ObservableProperty]
    private string _hookStatusText = string.Empty;

    [ObservableProperty]
    private string? _blockedText;

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
    private TrendMetric _trendMetric = TrendMetric.Average;

    [ObservableProperty]
    private bool _includeMidSession;

    [ObservableProperty]
    private bool _trendEmpty = true;

    [ObservableProperty]
    private string _trendExcludedText = string.Empty;

    public GameDetailViewModel(GameLibrary library, GameSelection selection, HookingConsent consent, IPageNavigator navigator,
        IConfirmations confirmations, IEditGamePrompt edit, IMessageStrip strip, ISessionSummaryOpener summaries,
        SessionSeriesLoader loader, IHardwareSnapshotRepository hardware, SessionSelection sessionSelection, IGamePicker picker, GuardBypass bypass)
    {
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        _bypass = bypass ?? throw new ArgumentNullException(nameof(bypass));
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

    public static string BypassLabel => Shared.Strings.Safety_Bypass_Toggle_Label;

    public static string BypassBody => Shared.Strings.Safety_Bypass_Toggle_Body;

    public static string BypassOnNotice => Shared.Strings.Safety_Bypass_On_Notice;

    public static string LifetimeLabel => Strings.GameDetail_LifetimeAvg;

    public static string SupportsHeader => Strings.GameDetail_Supports_Header;

    public static string SupportsEmptyText => Strings.GameDetail_Supports_Empty;

    public static string MeasuredHeader => Strings.GameDetail_Measured_Header;

    public static string HookingHeader => Strings.GameDetail_Hooking_Header;

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

    public static string LatencyHeader => Strings.Latency_Header;

    public static string LatencyEmptyText => Strings.Latency_Empty;

    public static string SensorsEmptyText => Strings.Sensors_Empty;

    public static IReadOnlyList<Choice<TrendMetric>> TrendMetrics { get; } =
    [
        new(TrendMetric.Average, Strings.Trend_Metric_Average),
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
        Present(detail);
        await RebuildTrendAsync(ct).ConfigureAwait(true);
    }

    partial void OnSelectedSessionChanged(SessionItemViewModel? value)
    {
        _selection.Set(value?.Id);    // File ▸ Export acts on this (P4 PR-3)
        Pending = LoadSelectedAsync(value);
    }

    partial void OnTrendMetricChanged(TrendMetric value) => RebuildTrendPoints();

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
    private Task ReEnableHookingAsync() => RunAsync(EnableAsync);

    [RelayCommand]
    private Task EditAsync() => RunAsync(EditCoreAsync);

    [RelayCommand]
    private Task RemoveAsync() => RunAsync(RemoveCoreAsync);

    [RelayCommand]
    private Task ChangeExecutableAsync() => RunAsync(ChangeExecutableCoreAsync);

    [RelayCommand]
    private Task ToggleGuardBypassAsync() => RunAsync(ToggleGuardBypassCoreAsync);

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
    /// Turning it ON is the disclosure, both of its acts, then the Agent's stamp; turning it OFF needs no dialog. Either
    /// way the row is re-read, so the switch shows what is recorded and never what was clicked.
    /// </summary>
    private async Task ToggleGuardBypassCoreAsync()
    {
        if (Game is null)
        {
            return;
        }

        Busy = true;
        try
        {
            GuardBypassResult result = GuardBypassOn
                ? await _bypass.DisableAsync(Game.Id).ConfigureAwait(true)
                : await _bypass.EnableAsync(Game.Id, Game.Name, Game.HookBlockedReason).ConfigureAwait(true);
            (NoticeText, NoticeSeverity) = result.Outcome switch
            {
                GuardBypassOutcome.AgentUnavailable => (Shared.Strings.Safety_Consent_AgentUnavailable, InfoBarSeverity.Warning),
                GuardBypassOutcome.Failed => (string.Format(CultureInfo.CurrentCulture, Shared.Strings.Safety_Bypass_Failed_Format, result.Detail ?? Strings.Common_NotAvailable), InfoBarSeverity.Warning),
                _ => (null, InfoBarSeverity.Informational),
            };
            OnPropertyChanged(nameof(NoticeVisible));
        }
        finally
        {
            Busy = false;
        }

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
            HookingConsentOutcome.Failed => (result.Detail ?? Strings.Common_NotAvailable, InfoBarSeverity.Warning),
            _ => (null, InfoBarSeverity.Informational),
        };
        OnPropertyChanged(nameof(NoticeVisible));
    }

    private void Present(GameDetail detail)
    {
        GameRow row = detail.Row;
        Name = row.Name;
        Subtitle = string.Join(" · ", new[] { row.Publisher, row.GameVersion, Formats.Platform(row.Platform), EngineText(row) }.Where(static s => !string.IsNullOrWhiteSpace(s)));
        ExecutableText = row.Fingerprint.ExePath;

        SessionRow? last = detail.Sessions.Count > 0 ? detail.Sessions[0] : null;
        SessionRow? lastHooked = detail.Sessions.FirstOrDefault(static s => s.Tier == CaptureTier.Hooked && s.FrameCount > 0);
        LastSessionText = last is null ? string.Empty : string.Format(CultureInfo.CurrentCulture, Strings.GameDetail_LastSession_Format, Formats.Date(last.StartedAt), Formats.Api(last.Api));
        Lifetime = lastHooked is null ? FpsReadoutModel.Unavailable : FpsPresentation.FromRow(lastHooked);

        PresentChips(detail, lastHooked);
        PresentSupports(row);
        PresentMeasured(last, lastHooked);
        PresentHooking(row);
        PresentSessions(detail);
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
        GuardBypassOn = row.GuardBypassAt is not null;
        bool blocked = row.HookBlockedReason is not null || string.Equals(row.HookPrescanState, "blocked", StringComparison.Ordinal);

        // A blocked row's toggle is disabled because enabling it could only be refused; under the user's bypass it is
        // not refused, so the toggle works and the block's text stays on the page beside it.
        HookToggleEnabled = (!blocked || GuardBypassOn) && !Busy;
        HookStatusText = row.HookEnabled ? Strings.GameDetail_Hooking_On : Strings.GameDetail_Hooking_Off;
        // "Hooking is disabled for this game" beside a hooking switch that is ON was the beta.3 page under a bypass. The
        // finding stays on the page - it is still true - and says what happened to it.
        BlockedText = !blocked
            ? null
            : string.Format(CultureInfo.CurrentCulture,
                GuardBypassOn ? Shared.Strings.Safety_Bypass_Found_Overruled_Format : Shared.Strings.Safety_Blocked_Toggle_Format,
                row.HookBlockedReason ?? row.HookPrescanState);
        AutoDisabledText = row.HookAutoDisabledReason is { Length: > 0 } reason && !row.HookEnabled
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
        row.Engine is null ? string.Empty : row.EngineVersion is null ? row.Engine : row.Engine + " " + row.EngineVersion;

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

    private static string CapabilityName(string token) => token switch
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
