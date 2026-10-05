// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Recording;

/// <summary>
/// An engine's crash reporter seen starting as a child of a process (beta.14): its image, its parent's pid and — when the
/// watcher had seen the parent — the parent's image and start, and its own start.
/// </summary>
public sealed record CrashReporterSighting(string Image, int ParentPid, string? ParentImagePath, DateTimeOffset? ParentStartedAt, DateTimeOffset StartedAt);
