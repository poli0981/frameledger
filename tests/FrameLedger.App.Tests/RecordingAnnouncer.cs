// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary>What would have been said to a screen reader, in order (beta.35, D89).</summary>
internal sealed class RecordingAnnouncer : IAnnouncer
{
    public List<(string Text, AnnouncementKind Kind, string Activity)> Said { get; } = [];

    public void Announce(string text, AnnouncementKind kind, string activity) => Said.Add((text, kind, activity));
}
