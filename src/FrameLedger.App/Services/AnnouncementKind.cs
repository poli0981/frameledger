// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>How a screen reader treats an announcement (beta.35, D89).</summary>
public enum AnnouncementKind
{
    /// <summary>A result or a change of state: read in turn, a newer one of the same kind replacing one not yet read.</summary>
    Status,

    /// <summary>Something the user must know — a safety notice, a failure, the agent gone: every one is read.</summary>
    Alert,
}
