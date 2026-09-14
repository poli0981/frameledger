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

    public DashboardViewModel(IAgentLink agent, GameLibrary library, GameSelection selection, IPageNavigator navigator)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _agent.Changed += OnAgentChanged;
        _agent.EventReceived += OnAgentEvent;
        Refresh();
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
            case IpcMessageType.SessionCompleted when IpcCodec.Payload<SessionCompletedEvent>(envelope) is { } completed:
                Live.Stop(completed);
                Pending = LoadAsync();
                break;
            default:
                break;
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
        RunningSessions = status?.ActiveSessions.Count.ToString(CultureInfo.CurrentCulture) ?? Strings.Common_NotAvailable;
    }
}
