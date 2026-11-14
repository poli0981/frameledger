// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Charts;

/// <summary>What happened to a controller at a moment of a session (0.1.0-beta.50): the marks on the session's charts.</summary>
public enum InputEventKind
{
    /// <summary>A controller connected after the session's first reading.</summary>
    Connected,

    /// <summary>A controller dropped.</summary>
    Disconnected,

    /// <summary>A controller's battery fell to low: XInput's "low" or "empty", or 10 % and under.</summary>
    BatteryLow,
}
