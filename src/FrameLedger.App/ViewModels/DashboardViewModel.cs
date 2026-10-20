// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

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
/// cards fed by the pipe's events — one per running session since beta.28 —, the recent sessions, and the totals strip.
/// </summary>
/// <remarks>
/// <b>It listens only while its page is on screen (beta.23):</b> <see cref="Attach"/> on Loaded, <see cref="Detach"/> on
/// Unloaded, as the Games and game pages do. It subscribed in its constructor and unsubscribed in a Dispose nothing called —
/// the root container kept every Dashboard ever opened, each answering every event and reloading its lists on the UI thread
/// at every session's end. FR-3.8's notice, which it raised, is <see cref="SessionNotices"/>'s.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IAgentLink _agent;
    private readonly GameLibrary _library;
    private readonly GameSelection _selection;
    private readonly IPageNavigator _navigator;
    private readonly ISessionSummaryOpener _summaries;
    private readonly UiThread _ui = new();
    private bool _attached;

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

    private readonly LiveSessions? _sessions;

    public DashboardViewModel(IAgentLink agent, GameLibrary library, GameSelection selection, IPageNavigator navigator, ISessionSummaryOpener summaries,
        LiveSessions? sessions = null)
    {
        _sessions = sessions;
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _summaries = summaries ?? throw new ArgumentNullException(nameof(summaries));
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

    /// <summary>
    /// The live capture cards: one per session the Agent is running (beta.28 — the owner, 2026-10-20: the Dashboard when two
    /// or more games run at once), hooked sessions first and each kind in the order it started. Until beta.28 there was one
    /// card, which a hooked session took from an unhooked one, and a second game showed only as the status card's count.
    /// </summary>
    public ObservableCollection<LiveCaptureViewModel> LiveCards { get; } = [];

    /// <summary>No session is running: one card says nothing is being captured.</summary>
    [ObservableProperty]
    private bool _liveIdle = true;

    /// <summary>"2 games recording" beside the header from two sessions on; null for one or none.</summary>
    [ObservableProperty]
    private string? _liveCountText;

    /// <summary>The cards side by side from two sessions on, two to a row.</summary>
    [ObservableProperty]
    private int _liveColumns = 1;

    public ObservableCollection<RecentSessionViewModel> Recent { get; } = [];

    /// <summary>The load in flight, for a test to await.</summary>
    public Task Pending { get; private set; }

    /// <summary>
    /// Listens to the Agent and to the running sessions while the page is on screen (beta.23): the page calls it on Loaded
    /// and <see cref="Detach"/> on Unloaded. What changed before it listened is read again here.
    /// </summary>
    public void Attach()
    {
        if (_attached)
        {
            return;
        }

        _agent.Changed += OnAgentChanged;
        _agent.EventReceived += OnAgentEvent;
        if (_sessions is not null)
        {
            _sessions.Changed += OnSessionsChanged;
        }

        _attached = true;
        Refresh();
        SeedLive();
    }

    public void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _agent.Changed -= OnAgentChanged;
        _agent.EventReceived -= OnAgentEvent;
        if (_sessions is not null)
        {
            _sessions.Changed -= OnSessionsChanged;
        }

        _attached = false;
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
    /// Every session already running has its card (2026-09-23; one card each since beta.28): the Dashboard is rebuilt on
    /// every visit, so one that started while another page was open had no start event here and read "Nothing is being
    /// captured.". A card with no name yet — made by a tick that came before the session was known — gets its name here.
    /// </summary>
    private void SeedLive()
    {
        foreach (RunningSession session in _sessions?.Current ?? [])
        {
            if (Find(session.SessionGuid) is { } card)
            {
                if (card.GameName.Length == 0)
                {
                    card.Seed(session);
                }

                continue;
            }

            var seeded = new LiveCaptureViewModel(session.SessionGuid);
            seeded.Seed(session);
            LiveCards.Add(seeded);
        }

        Arrange();
    }

    /// <summary>The four session events drive the cards, each its own session's; a completed session reloads the lists (07_IPC §Client behavior: the row is in SQLite).</summary>
    private void Dispatch(IpcEnvelope envelope)
    {
        switch (envelope.Type)
        {
            case IpcMessageType.SessionStarted when IpcCodec.Payload<SessionStartedEvent>(envelope) is { } started:
                CardFor(started.SessionGuid).Start(started);
                Arrange();
                break;
            case IpcMessageType.SessionProgress when IpcCodec.Payload<SessionProgressEvent>(envelope) is { } progress:
                // A tick is hooked: a session that attached since it started moves among the hooked ones.
                CardFor(progress.SessionGuid).Update(progress);
                Arrange();
                break;
            case IpcMessageType.SessionHeld when IpcCodec.Payload<SessionHeldEvent>(envelope) is { } held:
                CardFor(held.SessionGuid).Hold(held);
                break;
            case IpcMessageType.SessionCompleted when IpcCodec.Payload<SessionCompletedEvent>(envelope) is { } completed:
                // FR-3.8's notice is SessionNotices' (beta.23); the cards and the lists are this page's.
                if (Find(completed.SessionGuid) is { } ended)
                {
                    ended.Stop(completed);
                    LiveCards.Remove(ended);
                }

                Arrange();
                Pending = LoadAsync();
                break;
            default:
                break;
        }
    }

    private LiveCaptureViewModel? Find(Guid sessionGuid) => LiveCards.FirstOrDefault(c => c.SessionGuid == sessionGuid);

    /// <summary>
    /// The card of <paramref name="sessionGuid"/>, made when its first event arrives: named from the running sessions when
    /// they know it, else by its seed when they do (<see cref="SeedLive"/>).
    /// </summary>
    private LiveCaptureViewModel CardFor(Guid sessionGuid)
    {
        if (Find(sessionGuid) is { } card)
        {
            return card;
        }

        card = new LiveCaptureViewModel(sessionGuid);
        if (_sessions?.Current.FirstOrDefault(s => s.SessionGuid == sessionGuid) is { } running)
        {
            card.Seed(running);
        }

        LiveCards.Add(card);
        return card;
    }

    /// <summary>Hooked sessions first, each kind in the order it came — moved, never rebuilt, so a card keeps its bindings.</summary>
    private void Arrange()
    {
        List<LiveCaptureViewModel> ordered = [.. LiveCards.OrderBy(static c => c.Tier == 1 ? 0 : 1)];
        for (int i = 0; i < ordered.Count; i++)
        {
            int at = LiveCards.IndexOf(ordered[i]);
            if (at != i)
            {
                LiveCards.Move(at, i);
            }
        }

        LiveIdle = LiveCards.Count == 0;
        LiveColumns = LiveCards.Count >= 2 ? 2 : 1;
        LiveCountText = LiveCards.Count >= 2 ? string.Format(CultureInfo.CurrentCulture, Strings.Dashboard_Live_Count_Format, LiveCards.Count) : null;
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
