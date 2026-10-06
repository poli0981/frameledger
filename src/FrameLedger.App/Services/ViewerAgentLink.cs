// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Services;

/// <summary>
/// The Agent link of a viewer (beta.15, D52): never connected, never connecting, and with nothing behind it that could
/// connect — no pipe client, no launcher. Every feature that asks the Agent checks <see cref="IsConnected"/> first and
/// answers that the Agent is unavailable; one that asked anyway gets the exception a disconnected link throws.
/// </summary>
public sealed class ViewerAgentLink : IAgentLink
{
    public AgentConnectionState State => AgentConnectionState.Viewer;

    public StatusAck? Status => null;

    public HelloAck? Hello => null;

    public bool IsConnected => false;

    /// <summary>Never raised: the state never changes.</summary>
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    /// <summary>Never raised: there is no Agent to send one.</summary>
    public event EventHandler<AgentEventArgs>? EventReceived
    {
        add { }
        remove { }
    }

    public Task<IpcEnvelope> RequestAsync<TRequest>(string type, TRequest payload, CancellationToken ct = default)
        where TRequest : class =>
        throw new InvalidOperationException($"a viewer (--data-dir) has no Agent; {type} was not sent");

    /// <summary>Nothing to hold: a viewer never starts an Agent.</summary>
    public void SetLaunchHold(bool hold)
    {
    }
}
