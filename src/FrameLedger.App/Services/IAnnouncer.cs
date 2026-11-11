// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>
/// Tells a screen reader what just appeared (beta.35, owner decision D89): a safety notice, a banner, a short message at the
/// bottom of the window, a result. A screen reader reads what has the focus; a notice that appears elsewhere was never
/// heard. The words are the ones the window shows, in its language.
/// </summary>
public interface IAnnouncer
{
    /// <summary>Says <paramref name="text"/> to a screen reader, if one listens.</summary>
    /// <param name="text">The words, as shown.</param>
    /// <param name="kind">How the screen reader treats them.</param>
    /// <param name="activity">What kind of thing speaks (<c>Notice</c>, <c>Strip</c>, <c>Agent</c>…): a screen reader groups announcements by it.</param>
    void Announce(string text, AnnouncementKind kind, string activity);
}
