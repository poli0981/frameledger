// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

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
    /// <exception cref="InvalidOperationException">Not connected: nothing was sent.</exception>
    /// <exception cref="Infrastructure.Ipc.IpcRequestException">The Agent answered <c>Error</c>.</exception>
    /// <exception cref="TimeoutException">No answer within the request timeout: whether it was carried out is not known.</exception>
    /// <exception cref="System.IO.IOException">
    /// The connection closed before the Agent answered — it stopped, or restarted for an update (beta.22: every way the pipe
    /// closes is this one exception, <see cref="AgentConnection.AnsweredAsync"/>): whether it was carried out is not known.
    /// <see cref="AgentAnswers.IsUnanswered"/> names this and the timeout.
    /// </exception>
    Task<IpcEnvelope> RequestAsync<TRequest>(string type, TRequest payload, CancellationToken ct = default)
        where TRequest : class;
}
