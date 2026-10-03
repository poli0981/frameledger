// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>FR-6.2's blocking question: the selected sessions measured different things — compare across tiers anyway?</summary>
public interface IMixedTierPrompt
{
    Task<bool> AcknowledgeAsync(CancellationToken ct = default);
}
