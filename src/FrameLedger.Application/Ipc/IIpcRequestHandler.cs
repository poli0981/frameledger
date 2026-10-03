// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Ipc;

/// <summary>
/// Answers one request envelope with one ack envelope, already encoded. The pipe server decodes the frame,
/// checks the client's identity, and hands the envelope here; it does not know what any message means.
/// </summary>
public interface IIpcRequestHandler
{
    /// <summary>The ack — <c>Error</c> when the type is unknown or the payload is not what the type needs.</summary>
    ValueTask<byte[]> HandleAsync(IpcEnvelope request, CancellationToken ct);
}
