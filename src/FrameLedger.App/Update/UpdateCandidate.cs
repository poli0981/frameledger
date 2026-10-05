// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Update;

/// <summary>
/// A release newer than the installed one, as the feed describes it: the version, its notes (the GitHub release body,
/// Markdown), the full package's size, and — since beta.14 — the size of the delta packages from the installed version
/// when the feed has them (<see cref="DeltaBytes"/>), which is what Velopack downloads unless they fail to apply.
/// </summary>
public sealed record UpdateCandidate(string Version, string? NotesMarkdown, long SizeBytes)
{
    /// <summary>The deltas' size together, or null when there is none and the full package is the download.</summary>
    public long? DeltaBytes { get; init; }
}
