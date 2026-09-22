using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrameLedger.App.Pages;
using FrameLedger.App.Services;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.ViewModels;

/// <summary>
/// The Dashboard (08_UI §Dashboard): the Agent status card (what <c>HelloAck</c> and <c>StatusAck</c> said, every
/// value <c>N/A</c> until the Agent answered — never a placeholder that reads as a measurement), the live capture
/// card fed by the pipe's events, the recent sessions, and the totals strip.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly IAgentLink _agent;
    private readonly GameLibrary _library;
    private readonly GameSelection _selection;
    private readonly IPageNavigator _navigator;
    private readonly ISessionSummaryOpener _summaries;
    private readonly IMessageStrip _strip;
    private readonly UiThread _ui = new();

    [ObservableProperty]
    private string _agentState = Strings.Agent_State_Connecting;

    [ObservableProperty]
    private string _agentVersion = Strings.Common_NotAvailable;

    [ObservableProperty]
    private string _telemetrySource = Strings.Common_NotAvailable;

    [ObservableProperty]
    private string _overlayBuildId = Strings.Common_NotAvailable;

    [ObservableProperty]
    private string _elevated = Strings.Common_NotAvailable;

    [ObservableProperty]
    private string _runningSessions = Strings.Common_NotAvailable;

    [ObservableProperty]
    private string _gamesTrackedText = Strings.Common_NotAvailable;

    [ObservableProperty]
    private string _playtimeText = Strings.Common_NotAvailable;

    [ObservableProperty]
    private string _thisWeekText = Strings.Common_NotAvailable;

    [ObservableProperty]
    private bool _recentEmpty = true;

    private readonly IShellPresence? _presence;
    private readonly LiveSessions? _sessions;

    public DashboardViewModel(IAgentLink agent, GameLibrary library, GameSelection selection, IPageNavigator navigator, ISessionSummaryOpener summaries, IMessageStrip strip, IShellPresence? presence = null,
        LiveSessions? sessions = null)
    {
        _presence = presence;
        _sessions = sessions;
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _summaries = summaries ?? throw new ArgumentNullException(nameof(summaries));
        _strip = strip ?? throw new ArgumentNullException(nameof(strip));
        _agent.Changed += OnAgentChanged;
        _agent.EventReceived += OnAgentEvent;
        if (_sessions is not null)
        {
            _sessions.Changed += OnSessionsChanged;
        }

        Refresh();
        SeedLive();
        Pending = LoadAsync();
    }

    public static string Header => Strings.Dashboard_Header;

    public static string AgentHeader => Strings.Dashboard_Agent_Header;

    public static string EmptyText => Strings.Dashboard_Empty;

    public static string RecentHeader => Strings.Dashboard_Recent_Header;

    public static string TotalsGames => Strings.Dashboard_Totals_Games;

    public static string TotalsPlaytime => Strings.Dashboard_Totals_Playtime;

    public static string TotalsThisWeek => Strings.Dashboard_Totals_ThisWeek;

    public LiveCaptureViewModel Live { get; } = new();

    public ObservableCollection<RecentSessionViewModel> Recent { get; } = [];

    /// <summary>The load in flight, for a test to await.</summary>
    public Task Pending { get; private set; }

    public void Dispose()
    {
        _agent.Changed -= OnAgentChanged;
        _agent.EventReceived -= OnAgentEvent;
        if (_sessions is not null)
        {
            _sessions.Changed -= OnSessionsChanged;
        }
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IReadOnlyList<RecentSession> recent = await _library.RecentAsync(10, ct).ConfigureAwait(true);
        LibraryTotals totals = await _library.TotalsAsync(ct).ConfigureAwait(true);
        Recent.Clear();
        foreach (RecentSession r in recent)
        {
            Recent.Add(new RecentSessionViewModel(r));
        }

        RecentEmpty = Recent.Count == 0;
        GamesTrackedText = totals.GamesTracked.ToString(CultureInfo.CurrentCulture);
        PlaytimeText = Formats.Playtime(totals.TotalSeconds);
        ThisWeekText = totals.SessionsThisWeek.ToString(CultureInfo.CurrentCulture);
    }

    /// <summary>08_UI §Dashboard: a recent session opens its summary; the game page is one click further.</summary>
    [RelayCommand]
    private void OpenSession(RecentSessionViewModel? session)
    {
        if (session is not null)
        {
            _summaries.Open(session.Id);
        }
    }

    [RelayCommand]
    private void OpenGame(RecentSessionViewModel? session)
    {
        if (session is null)
        {
            return;
        }

        _selection.GameId = session.GameId;
        _navigator.Navigate<GameDetailPage>();
    }

    private void OnAgentChanged(object? sender, EventArgs e) => _ui.Post(Refresh);

    private void OnAgentEvent(object? sender, AgentEventArgs e) => _ui.Post(() => Dispatch(e.Envelope));

    private void OnSessionsChanged(object? sender, EventArgs e) => _ui.Post(() =>
    {
        SeedLive();
        Refresh();
    });

    /// <summary>
    /// A session already running takes the idle card (2026-09-23): the Dashboard is rebuilt on every visit, so one that
    /// started while another page was open had no start event here and read "Nothing is being captured.". A hooked
    /// session is preferred over one recorded without measuring.
    /// </summary>
    private void SeedLive()
    {
        if (Live.IsActive || _sessions?.Current is not { Count: > 0 } running)
        {
            return;
        }

        Live.Seed(running.FirstOrDefault(static s => s.Tier == 1) ?? running[0]);
    }

    /// <summary>The three session events drive the live card; a completed session reloads the lists (07_IPC §Client behavior: the row is in SQLite).</summary>
    private void Dispatch(IpcEnvelope envelope)
    {
        switch (envelope.Type)
        {
            case IpcMessageType.SessionStarted when IpcCodec.Payload<SessionStartedEvent>(envelope) is { } started:
                Live.Start(started);
                break;
            case IpcMessageType.SessionProgress when IpcCodec.Payload<SessionProgressEvent>(envelope) is { } progress:
                Live.Update(progress);
                break;
            case IpcMessageType.SessionHeld when IpcCodec.Payload<SessionHeldEvent>(envelope) is { } held:
                Live.Hold(held);
                break;
            case IpcMessageType.SessionCompleted when IpcCodec.Payload<SessionCompletedEvent>(envelope) is { } completed:
                // The session's own name (2026-09-23): the card may show another session, and a Tier-2 one had none here.
                string? name = completed.GameName ?? (Live.SessionGuid == completed.SessionGuid ? Live.GameName : null);
                Live.Stop(completed);
                Notice(completed, name);
                SeedLive();
                Pending = LoadAsync();
                break;
            default:
                break;
        }
    }

    /// <summary>FR-3.8's post-session notice, in-app while the window is on screen; the tray's balloon carries it otherwise (P3 PR-8b, one event → one channel). A safety stop is never a toast (08_UI §Notifications policy) — the shell's notices carry those.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture, which changes at runtime")]
    private void Notice(SessionCompletedEvent completed, string? name)
    {
        // A faulted session is the strip's CaptureError already; "discarded" would say something that did not happen.
        if (_presence is { IsShown: false } || string.Equals(completed.Finalize, "faulted", StringComparison.Ordinal))
        {
            return;
        }

        string game = name is { Length: > 0 } ? name : Strings.Common_NotAvailable;
        if (completed.SessionId is not null)
        {
            _strip.Success(Strings.Dashboard_Live_Header, string.Format(CultureInfo.CurrentCulture, Strings.Dashboard_SessionSaved_Format, game));
        }
        else
        {
            _strip.Info(Strings.Dashboard_Live_Header, string.Format(CultureInfo.CurrentCulture, Strings.Dashboard_SessionDiscarded_Format, game));
        }
    }

    private void Refresh()
    {
        AgentState = AgentStatusPresentation.Pill(_agent.State).Text;
        HelloAck? hello = _agent.Hello;
        StatusAck? status = _agent.Status;
        AgentVersion = hello?.AgentVersion ?? Strings.Common_NotAvailable;
        TelemetrySource = hello?.TelemetrySource ?? Strings.Common_NotAvailable;
        OverlayBuildId = hello?.OverlayBuildId ?? Strings.Common_NotAvailable;
        Elevated = hello is null ? Strings.Common_NotAvailable : hello.Elevated ? Strings.Common_Yes : Strings.Common_No;
        RunningSessions = _sessions is not null && _agent.State == AgentConnectionState.Connected
            ? _sessions.Current.Count.ToString(CultureInfo.CurrentCulture)
            : status?.ActiveSessions.Count.ToString(CultureInfo.CurrentCulture) ?? Strings.Common_NotAvailable;
    }
}
