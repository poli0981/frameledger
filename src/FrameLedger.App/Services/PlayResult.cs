// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>One press of Play: what happened, which store's app has it, why it fell back, and the agent's own word on a failure.</summary>
public sealed record PlayResult(PlayOutcome Outcome, string? Store = null, PlayFallback Fallback = PlayFallback.None, string? Detail = null);
