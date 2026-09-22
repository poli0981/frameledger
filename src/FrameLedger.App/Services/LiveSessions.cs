using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Services;

/// <summary>
/// The sessions the Agent is running right now, kept for the App's whole life (2026-09-23): seeded from the status the
/// Agent answers on every (re)connect, then moved by <c>SessionStarted</c> and <c>SessionCompleted</c>. Until this date the
/// Dashboard, the tray and the updater each kept their own list from events alone — a Dashboard opened after a game
/// started (it is rebuilt on every visit) showed "Nothing is being captured.", the updater read a status hours old, and a
/// session the Agent never announced (every Tier-2 one) was in none of them.
/// </summary>
public sealed class LiveSessions : IDisposable
{
    private readonly IAgentLink _agent;
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, RunningSession> _sessions = [];

    public LiveSessions(IAgentLink agent)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _agent.Changed += OnAgentChanged;
        _agent.EventReceived += OnAgentEvent;
        Reseed();
    }

    /// <summary>Raised on any change, on the thread that caused it; a view model marshals it as it does the link's own events.</summary>
    public event EventHandler? Changed;

    /// <summary>A snapshot, oldest first.</summary>
    public IReadOnlyList<RunningSession> Current
    {
        get
        {
            lock (_lock)
            {
                return [.. _sessions.Values.OrderBy(static s => s.StartedAt)];
            }
        }
    }

    public void Dispose()
    {
        _agent.Changed -= OnAgentChanged;
        _agent.EventReceived -= OnAgentEvent;
    }

    // The link raises Changed with a fresh StatusAck whenever it (re)connects, and on every state it leaves Connected for.
    private void OnAgentChanged(object? sender, EventArgs e) => Reseed();

    private void Reseed()
    {
        StatusAck? status = _agent.State == AgentConnectionState.Connected ? _agent.Status : null;
        lock (_lock)
        {
            _sessions.Clear();
            foreach (ActiveSession a in status?.ActiveSessions ?? [])
            {
                _sessions[a.SessionGuid] = new RunningSession(a.SessionGuid, a.GameId, a.GameName, a.Tier, a.StartedAt, a.Hold);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnAgentEvent(object? sender, AgentEventArgs e)
    {
        switch (e.Envelope.Type)
        {
            case IpcMessageType.SessionStarted when IpcCodec.Payload<SessionStartedEvent>(e.Envelope) is { } started:
                lock (_lock)
                {
                    _sessions[started.SessionGuid] = new RunningSession(started.SessionGuid, started.GameId, started.GameName, started.Tier, started.StartedAt, started.Hold);
                }

                Changed?.Invoke(this, EventArgs.Empty);
                break;
            case IpcMessageType.SessionCompleted when IpcCodec.Payload<SessionCompletedEvent>(e.Envelope) is { } completed:
                bool removed;
                lock (_lock)
                {
                    removed = _sessions.Remove(completed.SessionGuid);
                }

                if (removed)
                {
                    Changed?.Invoke(this, EventArgs.Empty);
                }

                break;
            default:
                break;
        }
    }
}
