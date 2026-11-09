// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>
/// The eight colours a Windows contrast theme is made of (beta.35, D91) — what Settings ▸ Accessibility ▸ Contrast themes
/// lets the user edit, and what every colour of a contrast-themed window is one of.
/// </summary>
public enum ContrastRole
{
    /// <summary>The background of a window and of what is in it.</summary>
    Window,

    /// <summary>Text on <see cref="Window"/>; also borders and strokes.</summary>
    WindowText,

    /// <summary>A selected item's background; an accent's fill.</summary>
    Highlight,

    /// <summary>Text on <see cref="Highlight"/>.</summary>
    HighlightText,

    /// <summary>A link (Windows' "hyperlink" colour).</summary>
    HotTrack,

    /// <summary>Disabled text and controls.</summary>
    GrayText,

    /// <summary>A button's background.</summary>
    ButtonFace,

    /// <summary>A button's text.</summary>
    ButtonText,
}
