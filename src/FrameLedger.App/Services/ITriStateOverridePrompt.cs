// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Services;

/// <summary>FR-8.3's override dialog as a question; null when cancelled.</summary>
public interface ITriStateOverridePrompt
{
    Task<TriStateOverrideChoice?> AskAsync(TriStateChipModel chip, CancellationToken ct = default);
}
