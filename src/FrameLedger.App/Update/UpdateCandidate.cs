// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Update;

/// <summary>A release newer than the installed one, as the feed describes it: the version, its notes (the GitHub release body, Markdown), the full package's size.</summary>
public sealed record UpdateCandidate(string Version, string? NotesMarkdown, long SizeBytes);
