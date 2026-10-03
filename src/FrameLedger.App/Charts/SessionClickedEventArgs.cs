// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Charts;

/// <summary>A click on a trend landed on a session's points (beta.11, D39): which session.</summary>
public sealed class SessionClickedEventArgs(long sessionId) : EventArgs
{
    public long SessionId { get; } = sessionId;
}
