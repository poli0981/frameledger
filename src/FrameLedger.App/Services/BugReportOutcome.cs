// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>Where one run of the flow ended.</summary>
public sealed record BugReportOutcome(string? ZipPath, BugReportChoice Choice, bool Written);
