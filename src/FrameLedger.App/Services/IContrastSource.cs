// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>Whether Windows has a contrast theme on, its colours, and when either changes (beta.35, D91).</summary>
public interface IContrastSource
{
    /// <summary>A contrast theme is on (Settings ▸ Accessibility ▸ Contrast themes).</summary>
    bool IsOn { get; }

    /// <summary>Its colours, as Windows has them now — the user's own if edited.</summary>
    ContrastPalette Palette { get; }

    /// <summary>A contrast theme was turned on or off, or its colours were edited.</summary>
    event EventHandler? Changed;
}
