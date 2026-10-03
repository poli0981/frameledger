// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>08_UI §Shell, the tray's four states: idle / ● capturing (Tier 1) / ◐ recording only (Tier 2) / ⏸ paused.</summary>
public enum TrayState
{
    Idle = 0,
    Capturing,
    RecordingOnly,
    Paused,
}
