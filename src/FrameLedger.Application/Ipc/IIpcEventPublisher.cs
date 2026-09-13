namespace FrameLedger.Application.Ipc;

/// <summary>
/// The Agent → UI half of the pipe as the Application layer sees it (<c>07_IPC</c> §C, events without an id).
/// The one shipped adapter is the pipe server; under <c>--console</c> it is never started and publishes to nobody.
/// </summary>
/// <remarks>
/// <b>Must never block and must never throw into the session loop.</b> It is called from the loop's own task
/// (<c>04_CAPTURE</c> §Threading model), after a drain; an adapter queues and drops rather than waits.
/// </remarks>
public interface IIpcEventPublisher
{
    /// <summary>Whether anyone is listening — the gate on work that exists only to be shown, like the 1 Hz progress.</summary>
    bool HasClients { get; }

    /// <summary>Broadcast one event to every connected client.</summary>
    void Publish<T>(string type, T payload)
        where T : class;
}
