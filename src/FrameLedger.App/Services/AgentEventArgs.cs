// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Services;

/// <summary>One Agent → UI event off the pipe, undecoded: the envelope's <c>type</c> says which record the payload is.</summary>
public sealed class AgentEventArgs(IpcEnvelope envelope) : EventArgs
{
    public IpcEnvelope Envelope { get; } = envelope ?? throw new ArgumentNullException(nameof(envelope));
}
