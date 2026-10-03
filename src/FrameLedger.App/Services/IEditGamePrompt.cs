// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Services;

/// <summary>FR-1.3's edit dialog as a question: the current metadata in, the edited metadata out, or null when cancelled.</summary>
public interface IEditGamePrompt
{
    Task<GameMetadata?> EditAsync(GameMetadata current, string? provenanceJson, CancellationToken ct = default);
}
