// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FrameLedger.App.Charts;
using FrameLedger.App.Services;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// The Advanced page (owner decision D101; <c>08_UI</c> §Advanced): what the vendor interfaces make available, each
/// value under what it belongs to. <b>This PC</b> is what the Agent reads now — asked only while the page is on screen
/// (D107), never stored; <b>Session</b> is what a session recorded with the option on stored; <b>Sources</b> is every
/// entry of the catalog with whether it is read, or why it is not.
/// </summary>
/// <remarks>
/// Nothing here reads hardware: the Agent does, and a session's numbers come from the ledger. In a viewer there is no
/// Agent, so This PC says so and the page opens on Session.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed partial class AdvancedViewModel : ObservableObject
{
    /// <summary>How many of the newest sessions recorded with the option on the selector offers.</summary>
    public const int SessionsOffered = 50;

    private const int _sessionTab = 1;

    private readonly AdvancedMachineFeed _feed;
    private readonly ISessionRepository _sessions;
    private readonly IGameRepository _games;
    private readonly SessionSeriesLoader _loader;
    private readonly AdvancedSelection _selection;
    private readonly UiMode _mode;
    private readonly UiThread _ui = new();

    private IDisposable? _watch;
    private bool _attached;
    private IReadOnlyList<AdvancedFact>? _shownFacts;
    private AdvancedSources? _shownSources;
    private bool _sourcesShown;
    private bool _settingSession;
    private IReadOnlyList<SensorSeries> _sessionSeries = [];
    private IReadOnlyList<AdvancedSeriesInfo> _sessionDirectory = [];

    [ObservableProperty]
    private int _selectedTab;

    [ObservableProperty]
    private string _machineStatus = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<AdvancedFactGroup> _machineGroups = [];

    [ObservableProperty]
    private IReadOnlyList<AdvancedLiveRow> _liveRows = [];

    [ObservableProperty]
    private IReadOnlyList<AdvancedMetricOption> _liveMetrics = [];

    [ObservableProperty]
    private AdvancedMetricOption? _selectedLiveMetric;

    [ObservableProperty]
    private bool _liveBand;

    [ObservableProperty]
    private bool _liveChartEmpty = true;

    [ObservableProperty]
    private bool _showElevationHint;

    [ObservableProperty]
    private IReadOnlyList<AdvancedSessionOption> _sessionOptions = [];

    [ObservableProperty]
    private AdvancedSessionOption? _selectedSession;

    [ObservableProperty]
    private bool _noSessions;

    [ObservableProperty]
    private string? _sessionSources;

    [ObservableProperty]
    private IReadOnlyList<AdvancedFactGroup> _sessionGroups = [];

    [ObservableProperty]
    private IReadOnlyList<AdvancedStatRow> _sessionStats = [];

    [ObservableProperty]
    private bool _sessionHasNoStats;

    [ObservableProperty]
    private bool _sessionSeriesGone;

    [ObservableProperty]
    private IReadOnlyList<AdvancedMetricOption> _sessionMetrics = [];

    [ObservableProperty]
    private AdvancedMetricOption? _selectedSessionMetric;

    [ObservableProperty]
    private bool _sessionBand;

    [ObservableProperty]
    private bool _sessionChartEmpty = true;

    [ObservableProperty]
    private IReadOnlyList<AdvancedSourceRow> _sourceRows = [];

    [ObservableProperty]
    private string? _sourcesNow;

    public AdvancedViewModel(AdvancedMachineFeed feed, ISessionRepository sessions, IGameRepository games, SessionSeriesLoader loader, AdvancedSelection selection, UiMode? mode = null)
    {
        _feed = feed ?? throw new ArgumentNullException(nameof(feed));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _mode = mode ?? UiMode.Profile;
        if (_mode.IsViewer)
        {
            // A copy is opened to look at its sessions; there is no Agent to say anything about this PC.
            _selectedTab = _sessionTab;
            _machineStatus = Strings.Advanced_State_Viewer;
        }

        ShowSources(sources: null);
        Pending = LoadSessionsAsync();
    }

    public static string Header => Strings.Advanced_Header;

    public static string TabMachine => Strings.Advanced_Tab_Machine;

    public static string TabSession => Strings.Advanced_Tab_Session;

    public static string TabSources => Strings.Advanced_Tab_Sources;

    /// <summary>The load in flight — the sessions' list, or the selected session — for a test to await.</summary>
    public Task Pending { get; private set; }

    /// <summary>The lines of the live chart, as last presented.</summary>
    public IReadOnlyList<AdvancedChartLine> LiveLines { get; private set; } = [];

    /// <summary>The unit the live chart's values are in.</summary>
    public string LiveUnit { get; private set; } = string.Empty;

    /// <summary>The lines of the session's chart, as last presented.</summary>
    public IReadOnlyList<AdvancedChartLine> SessionLines { get; private set; } = [];

    /// <summary>The unit the session chart's values are in.</summary>
    public string SessionUnit { get; private set; } = string.Empty;

    /// <summary>The live chart's lines changed: the page draws them.</summary>
    public event EventHandler? LivePresented;

    /// <summary>The session chart's lines changed: the page draws them.</summary>
    public event EventHandler? SessionPresented;

    /// <summary>The page is on screen: start asking the Agent (D107 — only while it is), and show what is known.</summary>
    public void Attach()
    {
        if (_attached)
        {
            return;
        }

        _attached = true;
        _selection.Requested += OnSessionRequested;
        TakeRequestedSession();

        if (_mode.IsViewer)
        {
            return;
        }

        _feed.Changed += OnFeedChanged;
        _watch = _feed.Watch();
        ShowMachine(_feed.Current);
    }

    /// <summary>The page left the screen: stop asking, so the Agent lets its reader go.</summary>
    public void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _attached = false;
        _selection.Requested -= OnSessionRequested;
        _feed.Changed -= OnFeedChanged;
        _watch?.Dispose();
        _watch = null;
    }

    /// <summary>What the feed last learnt, shown. Public for a test, which has no feed loop to raise it.</summary>
    public void ShowMachine(AdvancedMachineView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        MachineStatus = view.State switch
        {
            AdvancedFeedState.Live when view.TakenAt is DateTimeOffset at =>
                string.Format(CultureInfo.CurrentCulture, Strings.Advanced_State_Live, at.ToLocalTime().ToString("T", CultureInfo.CurrentCulture)),
            AdvancedFeedState.Off => Strings.Advanced_State_Off,
            AdvancedFeedState.NoAgent => Strings.Advanced_State_NoAgent,
            AdvancedFeedState.OlderAgent => Strings.Advanced_State_OlderAgent,
            _ => Strings.Advanced_State_Waiting,
        };

        if (!ReferenceEquals(view.Facts, _shownFacts))
        {
            _shownFacts = view.Facts;
            MachineGroups = AdvancedPresenter.Groups(view.Facts);
        }

        LiveRows = AdvancedPresenter.Live(view);
        IReadOnlyList<AdvancedMetricOption> metrics = AdvancedPresenter.Metrics(view.Values.Keys, view.Series);
        if (!metrics.SequenceEqual(LiveMetrics))
        {
            string? keep = SelectedLiveMetric?.Id;
            LiveMetrics = metrics;
            SelectedLiveMetric = metrics.FirstOrDefault(m => string.Equals(m.Id, keep, StringComparison.Ordinal)) ?? (metrics.Count > 0 ? metrics[0] : null);
        }

        ShowElevationHint = view.Sources.ElevatedSensors is AdvancedSourceStatus.NotElevated or AdvancedSourceStatus.PawnIoMissing;
        ShowSources(view.State == AdvancedFeedState.Live ? view.Sources : null);
        PresentLive(view);
    }

    partial void OnSelectedLiveMetricChanged(AdvancedMetricOption? value) => PresentLive(_feed.Current);

    partial void OnSelectedSessionChanged(AdvancedSessionOption? value)
    {
        if (!_settingSession)
        {
            Pending = LoadSessionAsync(value?.SessionId);
        }
    }

    partial void OnSelectedSessionMetricChanged(AdvancedMetricOption? value) => PresentSession();

    private void OnSessionRequested(object? sender, EventArgs e) => _ui.Post(TakeRequestedSession);

    /// <summary>A session's summary asked for it: the list is read again - it may be a session finished since - and that one shown.</summary>
    private void TakeRequestedSession()
    {
        if (_attached && _selection.Take() is long asked)
        {
            Pending = LoadSessionsAsync(asked);
        }
    }

    private void OnFeedChanged(object? sender, EventArgs e) => _ui.Post(() =>
    {
        if (_attached)
        {
            ShowMachine(_feed.Current);
        }
    });

    private void ShowSources(AdvancedSources? sources)
    {
        if (_sourcesShown && Equals(sources, _shownSources))
        {
            return;
        }

        _sourcesShown = true;
        _shownSources = sources;
        SourceRows = AdvancedPresenter.Sources(sources);
        SourcesNow = sources is null ? null : AdvancedPresenter.SourcesLine(sources);
    }

    private void PresentLive(AdvancedMachineView view)
    {
        IReadOnlyList<AdvancedChartLine> lines = [];
        bool band = false;
        string unit = string.Empty;
        if (SelectedLiveMetric is { } metric)
        {
            unit = AdvancedLabels.Unit(AdvancedMetricCatalog.Find(metric.Id)?.Unit ?? AdvancedUnit.None);
            var series = new List<SensorSeries>();
            foreach (string name in view.Values.Keys)
            {
                if (string.Equals(AdvancedPresenter.Resolve(name, view.Series).Metric?.Id, metric.Id, StringComparison.Ordinal))
                {
                    IReadOnlyList<AdvancedLivePoint> points = _feed.HistoryOf(name);
                    series.Add(new SensorSeries(name, [.. points.Select(static p => p.At.LocalDateTime.ToOADate())], [.. points.Select(static p => p.Value)]));
                }
            }

            (lines, band) = AdvancedPresenter.Lines(metric.Id, series, view.Series);
        }

        LiveLines = lines;
        LiveUnit = unit;
        LiveBand = band;
        LiveChartEmpty = lines.Count == 0;
        LivePresented?.Invoke(this, EventArgs.Empty);
    }

    private void PresentSession()
    {
        IReadOnlyList<AdvancedChartLine> lines = [];
        bool band = false;
        string unit = string.Empty;
        if (SelectedSessionMetric is { } metric)
        {
            unit = AdvancedLabels.Unit(AdvancedMetricCatalog.Find(metric.Id)?.Unit ?? AdvancedUnit.None);
            (lines, band) = AdvancedPresenter.Lines(metric.Id, _sessionSeries, _sessionDirectory);
        }

        SessionLines = lines;
        SessionUnit = unit;
        SessionBand = band;
        SessionChartEmpty = lines.Count == 0;
        SessionPresented?.Invoke(this, EventArgs.Empty);
    }

    private async Task LoadSessionsAsync(long? asked = null)
    {
        IReadOnlyList<SessionRow> rows = await _sessions.ListAdvancedAsync(SessionsOffered).ConfigureAwait(true);
        var options = new List<AdvancedSessionOption>(rows.Count + 1);
        var names = new Dictionary<long, string>();
        foreach (SessionRow row in rows)
        {
            options.Add(await OptionOfAsync(row, names).ConfigureAwait(true));
        }

        if (asked is long id && !options.Exists(o => o.SessionId == id)
            && await _sessions.FindAdvancedAsync(id).ConfigureAwait(true) is not null
            && await _sessions.FindByIdAsync(id).ConfigureAwait(true) is { } older)
        {
            // Asked for from its summary, and older than the newest fifty: it is offered all the same.
            options.Add(await OptionOfAsync(older, names).ConfigureAwait(true));
        }

        long? keep = asked ?? SelectedSession?.SessionId;
        SessionOptions = options;
        NoSessions = options.Count == 0;
        AdvancedSessionOption? next = options.Find(o => o.SessionId == keep) ?? (options.Count > 0 ? options[0] : null);
        if (asked is not null && next?.SessionId == asked)
        {
            SelectedTab = _sessionTab;
        }

        // The selection is set without its own load, and the load is awaited here: one read of the session, in this task.
        _settingSession = true;
        SelectedSession = next;
        _settingSession = false;
        await LoadSessionAsync(next?.SessionId).ConfigureAwait(true);
    }

    private async Task<AdvancedSessionOption> OptionOfAsync(SessionRow row, Dictionary<long, string> names)
    {
        if (!names.TryGetValue(row.GameId, out string? name))
        {
            name = (await _games.FindByIdAsync(row.GameId).ConfigureAwait(true))?.Name ?? Strings.Common_NotAvailable;
            names[row.GameId] = name;
        }

        return new AdvancedSessionOption(row.Id, name + " — " + Formats.Date(row.StartedAt));
    }

    private async Task LoadSessionAsync(long? sessionId)
    {
        AdvancedSessionRow? row = sessionId is long id ? await _sessions.FindAdvancedAsync(id).ConfigureAwait(true) : null;
        IReadOnlyList<SensorSeries> series = sessionId is long loaded && row is not null ? await _loader.LoadAdvancedAsync(loaded).ConfigureAwait(true) : [];
        if (SelectedSession?.SessionId != sessionId)
        {
            return;   // another session was chosen while this one was read
        }

        if (row is null)
        {
            _sessionSeries = [];
            _sessionDirectory = [];
            SessionSources = null;
            SessionGroups = [];
            SessionStats = [];
            SessionHasNoStats = false;
            SessionSeriesGone = false;
            SessionMetrics = [];
            SelectedSessionMetric = null;
            PresentSession();
            return;
        }

        (IReadOnlyList<AdvancedFact> sessionFacts, AdvancedSources sources) = AdvancedJson.ParseSession(row.FactsJson);
        (IReadOnlyList<AdvancedSeriesInfo> directory, _) = AdvancedJson.ParseSeries(row.SeriesJson);
        IReadOnlyDictionary<string, SensorSeriesStats> stats = AdvancedJson.ParseStats(row.StatsJson);
        _sessionSeries = series;
        _sessionDirectory = directory;
        SessionSources = AdvancedPresenter.SourcesLine(sources);
        SessionGroups = AdvancedPresenter.Groups([.. AdvancedJson.ParseFacts(row.MachineFactsJson), .. sessionFacts]);
        SessionStats = AdvancedPresenter.Stats(stats, directory);
        SessionHasNoStats = stats.Count == 0;
        SessionSeriesGone = stats.Count > 0 && !series.Any(static s => s.Values.Length > 0);
        string? keep = SelectedSessionMetric?.Id;
        IReadOnlyList<AdvancedMetricOption> metrics = AdvancedPresenter.Metrics(series.Where(static s => s.Values.Length > 0).Select(static s => s.Name), directory);
        SessionMetrics = metrics;
        AdvancedMetricOption? next = metrics.FirstOrDefault(m => string.Equals(m.Id, keep, StringComparison.Ordinal)) ?? (metrics.Count > 0 ? metrics[0] : null);
        if (Equals(next, SelectedSessionMetric))
        {
            PresentSession();
        }
        else
        {
            SelectedSessionMetric = next;
        }
    }
}
