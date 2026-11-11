// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>
/// The session the Advanced page should show when it next opens: a session's summary names it, then navigates. One
/// slot, taken once — a page opened from the navigation pane afterwards shows its newest session as usual.
/// </summary>
public sealed class AdvancedSelection
{
    private long _sessionId;

    /// <summary>A session was asked for: a page already on screen takes it now.</summary>
    public event EventHandler? Requested;

    /// <summary>Asks the page to show <paramref name="sessionId"/> - now if it is on screen, else the next time it opens.</summary>
    public void Request(long sessionId)
    {
        Interlocked.Exchange(ref _sessionId, sessionId);
        Requested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The session asked for since the last call, or null.</summary>
    public long? Take() => Interlocked.Exchange(ref _sessionId, 0) is > 0 and long id ? id : null;
}
