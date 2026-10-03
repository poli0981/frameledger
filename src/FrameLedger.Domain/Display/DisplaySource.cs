// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Display;

/// <summary>
/// What a session's display facts rested on (<c>sessions.display_source</c>, schema 0016). Stored ids; the strongest one any
/// sample of the session had wins, because it decides which shares can be told apart.
/// </summary>
public static class DisplaySource
{
    /// <summary>A DXGI swap chain was asked whether it was exclusive: fullscreen and borderless can be told apart.</summary>
    public const string SwapChain = "swapchain";

    /// <summary>An OpenGL stream named its window; exclusivity cannot be asked.</summary>
    public const string OpenGl = "opengl";

    /// <summary>Only the window was read (Vulkan, a game that was not hooked); exclusivity cannot be asked.</summary>
    public const string Window = "window";

    /// <summary>How strong a source is: a session keeps the strongest its samples had.</summary>
    public static int Strength(string? source) => source switch
    {
        SwapChain => 3,
        OpenGl => 2,
        Window => 1,
        _ => 0,
    };
}
