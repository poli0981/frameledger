// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Domain.Display;

namespace FrameLedger.Application.Capture;

/// <summary>
/// The game's window, read out of the game's process (beta.10, <c>03_METRICS</c> §Display mode): the same class of question
/// the focus sample asks — documented user32 calls about a top-level window — and nothing belonging to the game (CLAUDE.md
/// rule 4 is about game memory). Infrastructure's <c>Display.WindowGeometryProbe</c> is the one implementation.
/// </summary>
public interface IDisplayProbe
{
    /// <summary>
    /// The window the game is shown in: <paramref name="hwnd"/> when the injected component named one and it still belongs
    /// to one of <paramref name="pids"/> (a window handle is reused after its window is gone), else the largest visible,
    /// uncloaked top-level window any of <paramref name="pids"/> owns. Null when there is none — a composition-only process,
    /// a game still loading, one on another desktop.
    /// </summary>
    /// <param name="pids">The game's processes: the pinned one when the loop holds it, else every process running its executable.</param>
    /// <param name="hwnd">The window the swap chain presents to, from shared-memory region 4; 0 when none was named.</param>
    WindowView? Observe(IReadOnlyCollection<int> pids, ulong hwnd);
}
