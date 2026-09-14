using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Services;

/// <summary>
/// The Agent connection as a view model sees it: the state, the last <c>StatusAck</c>, and the two events —
/// <see cref="Changed"/> on every state change and <see cref="EventReceived"/> for every Agent → UI envelope
/// (<c>07_IPC</c> §Events). Both fire on the thread pool; view models marshal through <see cref="UiThread"/>.
/// </summary>
public interface IAgentLink : IAgentRequests
{
    AgentConnectionState State { get; }

    StatusAck? Status { get; }

    event EventHandler? Changed;

    event EventHandler<AgentEventArgs>? EventReceived;
}
