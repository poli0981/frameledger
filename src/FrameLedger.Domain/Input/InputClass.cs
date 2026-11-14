// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Input;

/// <summary>
/// What a stretch of a session was played with (<c>03_METRICS</c> §Input): decided for each slice of time the game was
/// in front, from outside the game. It says a device was in use while the game was in front, never what the game did
/// with it.
/// </summary>
public enum InputClass
{
    /// <summary>Neither a controller nor the keyboard or mouse was in use.</summary>
    None = 0,

    /// <summary>A controller was in use and the keyboard and mouse were not.</summary>
    Controller,

    /// <summary>The keyboard or the mouse was in use and no controller was. The two are one class: only the time of the last input is read.</summary>
    KeyboardMouse,

    /// <summary>A controller and the keyboard or mouse were both in use.</summary>
    Both,
}
