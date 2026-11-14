// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Input;

namespace FrameLedger.Application.Recording;

/// <summary>What <see cref="InputSessionBuilder"/> makes of a session's input readings: the totals a session row stores, and the controllers as JSON.</summary>
public sealed record InputSessionSummary
{
    /// <summary>The time the reader ran and the session was not paused.</summary>
    public long ObservedMs { get; init; }

    /// <summary>Of that, the time a window of the game was in front: what every share is a share of.</summary>
    public long ForegroundMs { get; init; }

    public long ControllerMs { get; init; }

    public long KeyboardMouseMs { get; init; }

    public long BothMs { get; init; }

    public long IdleMs { get; init; }

    /// <summary>How many times the player went from a controller to the keyboard and mouse or back.</summary>
    public int MethodSwitches { get; init; }

    /// <summary>The sources that answered (<see cref="InputHealth.Sources"/>).</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary><c>sessions.input_devices</c>.</summary>
    public string DevicesJson { get; init; } = string.Empty;
}
