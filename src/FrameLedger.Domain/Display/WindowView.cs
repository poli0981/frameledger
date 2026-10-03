// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Display;

/// <summary>
/// What user32 said about the game's top-level window, read OUT of the game's process by the capture agent (beta.10): the
/// same class of question <c>GetForegroundWindow</c> answers for the focus sample, and nothing belonging to the game.
/// Coordinates are physical pixels on the virtual screen (the reader is per-monitor DPI aware while it asks).
/// </summary>
/// <param name="Minimized">The window was minimised (<c>IsIconic</c>).</param>
/// <param name="Client">The client area, in screen coordinates.</param>
/// <param name="Monitor">The whole monitor the window is on (not its work area).</param>
/// <param name="MonitorHz">The monitor's current refresh rate, or null when the mode could not be read.</param>
public readonly record struct WindowView(bool Minimized, ScreenRect Client, ScreenRect Monitor, int? MonitorHz)
{
    /// <summary>
    /// Whether the client area covers the whole monitor, within one pixel on each edge: what a borderless window and an
    /// exclusive-fullscreen one both do, and a maximised window with a title bar and a taskbar does not.
    /// </summary>
    public bool CoversMonitor =>
        !Monitor.IsEmpty
        && Client.Left <= Monitor.Left + 1
        && Client.Top <= Monitor.Top + 1
        && Client.Right >= Monitor.Right - 1
        && Client.Bottom >= Monitor.Bottom - 1;
}
