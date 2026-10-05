// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Recording;

/// <summary>One Application-log record as the matcher reads it: its id, its provider, and its properties in order (typed as the log gives them).</summary>
public sealed record CrashLogRecord(int EventId, string? Provider, IReadOnlyList<object?> Properties);
