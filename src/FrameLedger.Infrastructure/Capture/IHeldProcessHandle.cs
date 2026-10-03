// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Capture;

/// <summary>
/// A session's pin that lends its handle to a read about the process (beta.12, D43): <c>GameMemoryReader</c> reads the
/// game's own memory through the handle the session already holds for liveness — <c>SYNCHRONIZE |
/// PROCESS_QUERY_LIMITED_INFORMATION</c> and nothing more — so a pinned session opens nothing new for it.
/// </summary>
public interface IHeldProcessHandle
{
    /// <summary>The held handle. Borrowed for a call, never disposed or stored by the borrower.</summary>
    SafeHandle ProcessHandle { get; }
}
