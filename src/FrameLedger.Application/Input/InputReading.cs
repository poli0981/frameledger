// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Input;

/// <summary>
/// What the input reader saw for one session during one tick of the telemetry poller (<c>03_METRICS</c> §Input): how
/// the time the game was in front divides between a controller, the keyboard and mouse, both and neither, and each
/// controller's part in it.
/// </summary>
/// <remarks>
/// It holds times and counts. Which button, where a stick was, which key, what was typed and where the mouse was are
/// not in it because the reader never keeps them (<c>21_INPUT_DEVICES</c> §What is never read).
/// </remarks>
public sealed record InputReading
{
    /// <summary>The time the tick covers: every slice, in front or not, paused or not. What a battery level and a connection are timed by.</summary>
    public int SpanMs { get; init; }

    /// <summary>Of <see cref="SpanMs"/>, the time the session was not paused.</summary>
    public int ObservedMs { get; init; }

    /// <summary>Of <see cref="ObservedMs"/>, the time a window of the game was in front. The four below add up to it.</summary>
    public int ForegroundMs { get; init; }

    public int ControllerMs { get; init; }

    public int KeyboardMouseMs { get; init; }

    public int BothMs { get; init; }

    public int IdleMs { get; init; }

    /// <summary>Whether a pad a program presents - Steam's virtual gamepad - was among XInput's pads during the tick.</summary>
    public bool VirtualPad { get; init; }

    /// <summary>The controllers that were connected at some moment of the tick.</summary>
    public IReadOnlyList<InputDeviceReading> Devices { get; init; } = [];

    /// <summary>What each controller in <see cref="Devices"/> is. A reading stands alone: it names every device it counts.</summary>
    public IReadOnlyList<InputDevice> Table { get; init; } = [];

    /// <summary>What the reader's sources did since the session began reading.</summary>
    public InputHealth Health { get; init; } = new();
}
