// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>The newest session in the ledger, as the bug report offers it: which one, of what, and when.</summary>
public sealed record LastSessionInfo(long SessionId, string Game, DateTimeOffset StartedAt);
