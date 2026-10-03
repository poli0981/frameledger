// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Display;

/// <summary>
/// How a game was being shown at one sample (beta.10, <c>03_METRICS</c> §Display mode). The names are stored in no column —
/// each mode has its own milliseconds column — but they are the words the product uses.
/// </summary>
public enum DisplayMode
{
    /// <summary>No window of the game could be found or read at this sample.</summary>
    NoWindow = 0,

    /// <summary>The game's window was minimised.</summary>
    Minimized,

    /// <summary>The swap chain SAID exclusive fullscreen (<c>GetFullscreenState</c>, DXGI only).</summary>
    ExclusiveFullscreen,

    /// <summary>The window's client area covers its whole monitor and the swap chain said it was not exclusive.</summary>
    Borderless,

    /// <summary>
    /// The window's client area covers its whole monitor and nothing could say whether it was exclusive — OpenGL, Vulkan,
    /// a game that was not hooked. "Borderless or fullscreen", never counted as either.
    /// </summary>
    CoversScreen,

    /// <summary>The window's client area does not cover its monitor.</summary>
    Windowed,
}
