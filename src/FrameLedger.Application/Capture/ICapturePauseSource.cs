// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Capture;

/// <summary>
/// FR-3.9's global pause, as the capture loop reads it (P3 PR-1b): every drain tick compares it to what the
/// ring was last told and publishes <c>pauseRequested</c> on a change. Supervision — the guard scan, the
/// kill switch, the safety stop — never pauses; only the recording does.
/// </summary>
public interface ICapturePauseSource
{
    bool IsPaused { get; }
}
