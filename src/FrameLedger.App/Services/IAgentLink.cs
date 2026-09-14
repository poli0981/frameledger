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

    /// <summary>
    /// While held, a round that finds no Agent does not start one beside this executable (P4 PR-5: the update's
    /// apply asks the Agent to stop and must not have it restarted under the updater). Released on a failed apply.
    /// </summary>
    void SetLaunchHold(bool hold);
}
