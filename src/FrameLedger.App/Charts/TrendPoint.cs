// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Charts;

/// <summary>One session on the trend line: when, the metric's value, which session, and whether FR-6.4 excludes it by default.</summary>
public sealed record TrendPoint(DateTimeOffset At, double Value, long SessionId, bool SettingsChangedMidSession);
