// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Play;

/// <summary>The Play button's two modes (beta.21, owner decision D59): <c>ui.play_mode</c>'s choices.</summary>
public static class PlayMode
{
    /// <summary>Through the store that installed the game — the default; a game no store installed starts directly.</summary>
    public const string Store = "store";

    /// <summary>The capture agent starts the game itself, recorded from its first frame, never as administrator.</summary>
    public const string Direct = "direct";

    /// <summary>Whether a stored value is the direct mode; anything else is the store's.</summary>
    public static bool IsDirect(string? value) => string.Equals(value, Direct, StringComparison.Ordinal);
}
