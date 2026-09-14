using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.TriState;
using FrameLedger.Domain.Sessions;
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
    private readonly IMessageStrip _strip;
    private readonly ISessionSummaryOpener _summaries;
    private readonly long? _gameId;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _subtitle = string.Empty;

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

    public GameDetailViewModel(GameLibrary library, GameSelection selection, HookingConsent consent, IPageNavigator navigator,
        IConfirmations confirmations, IEditGamePrompt edit, IMessageStrip strip, ISessionSummaryOpener summaries)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        ArgumentNullException.ThrowIfNull(selection);
        _consent = consent ?? throw new ArgumentNullException(nameof(consent));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _confirmations = confirmations ?? throw new ArgumentNullException(nameof(confirmations));
        _edit = edit ?? throw new ArgumentNullException(nameof(edit));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
        _summaries = summaries ?? throw new ArgumentNullException(nameof(summaries));
        _gameId = selection.GameId;
        Pending = LoadAsync();
    }

    public static string BackText => Strings.GameDetail_Back;

    public static string EditText => Strings.GameDetail_Edit;

    public static string RemoveText => Strings.GameDetail_Remove;

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

    public static string LaterText => string.Format(CultureInfo.CurrentCulture, Strings.GameDetail_Tab_Later_Format, Strings.GameDetail_Tab_Frametime);

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
        NotFound = false;
        Present(detail);
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
        string upscaler = Formats.Upscaler(lastHooked.Upscaler);
        if (lastHooked.UpscalerQuality is { Length: > 0 } quality)
        {
            upscaler += " " + quality;
        }

        Measured.Add(upscaler + " · " + Formats.Resolution(lastHooked.RenderW, lastHooked.RenderH, lastHooked.OutputW, lastHooked.OutputH));
        FpsReadoutModel readout = FpsPresentation.FromRow(lastHooked);
        Measured.Add(readout.Kind == FpsReadoutKind.Generated
            ? Formats.FrameGeneration(lastHooked.FgMode) + " " + (readout.FactorChip ?? string.Empty)
            : Formats.FrameGeneration(lastHooked.FgMode));
        foreach (TriStateChipModel chip in Chips.Where(static c => !c.IsNotApplicable))
        {
            Measured.Add(chip.Text);
        }
    }

    private void PresentHooking(GameRow row)
    {
        HookEnabled = row.HookEnabled;
        bool blocked = row.HookBlockedReason is not null || string.Equals(row.HookPrescanState, "blocked", StringComparison.Ordinal);
        HookToggleEnabled = !blocked && !Busy;
        HookStatusText = row.HookEnabled ? Strings.GameDetail_Hooking_On : Strings.GameDetail_Hooking_Off;
        BlockedText = blocked ? string.Format(CultureInfo.CurrentCulture, Shared.Strings.Safety_Blocked_Toggle_Format, row.HookBlockedReason ?? row.HookPrescanState) : null;
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

    /// <summary><c>capability_flags</c> (<c>05_DETECTION</c> §Capability hints): an object of flags or an array of tokens → product names. Nothing writes it before P4; an unreadable value is no chips.</summary>
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
        "dlssg" => "DLSS-G",
        "dlssd" => "DLSS Ray Reconstruction",
        "fsr" => "FSR",
        "fsrfg" => "FSR Frame Generation",
        "xess" => "XeSS",
        "xefg" => "XeFG",
        "reflex" => "Reflex",
        _ => string.Empty,
    };
}
