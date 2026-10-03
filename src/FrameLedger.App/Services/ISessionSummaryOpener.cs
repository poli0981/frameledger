// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>Opens the session summary window for one session (a row click, a recent-list click, later the post-session toast).</summary>
public interface ISessionSummaryOpener
{
    void Open(long sessionId);
}
