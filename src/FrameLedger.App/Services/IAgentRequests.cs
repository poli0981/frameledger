using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Services;

/// <summary>
/// What a feature needs from the Agent connection to ASK for something (<c>07_IPC</c> §Messages, UI → Agent):
/// the <c>HelloAck</c> it answered with, and one request → one envelope. An interface so the consent flow can be
/// tested without a pipe; <see cref="AgentConnection"/> is the one implementation.
/// </summary>
public interface IAgentRequests
{
    /// <summary>The Agent's <c>HelloAck</c>, or null while not connected.</summary>
    HelloAck? Hello { get; }

    bool IsConnected { get; }

    /// <summary>
    /// Sends <paramref name="payload"/> as <paramref name="type"/> and returns whatever non-<c>Error</c> envelope
    /// answers it — the caller distinguishes the acks (a <c>HookEnabledAck</c> from a <c>Refused</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException">Not connected.</exception>
    /// <exception cref="Infrastructure.Ipc.IpcRequestException">The Agent answered <c>Error</c>.</exception>
    /// <exception cref="TimeoutException">No answer within the request timeout.</exception>
    Task<IpcEnvelope> RequestAsync<TRequest>(string type, TRequest payload, CancellationToken ct = default)
        where TRequest : class;
}
