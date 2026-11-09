// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>Says nothing: where no screen reader is wired (tests, a viewer's pieces built by hand).</summary>
public sealed class NoAnnouncer : IAnnouncer
{
    public static readonly NoAnnouncer Instance = new();

    public void Announce(string text, AnnouncementKind kind, string activity)
    {
    }
}
