// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Input;

/// <summary>
/// The Agent's one reader of game controllers and of keyboard-and-mouse activity (owner decisions of 2026-11-14,
/// <c>21_INPUT_DEVICES</c>). It reads from outside every game - nothing is hooked, nothing in a game is opened - and
/// only while a session holds a view of it: with no session it does not run at all.
/// </summary>
public interface IInputDevices
{
    /// <summary>One session's view of the reader. The reader runs from the first view opened to the last disposed.</summary>
    IInputActivitySource Open();
}
