// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>
/// The admin mode's disclosure as a question (beta.10, owner decision D34): shows the versioned text and answers whether the
/// user understood and chose to have the Agent started as administrator. The WPF implementation is
/// <see cref="AgentAdminPrompt"/>; a test substitutes an answer.
/// </summary>
public interface IAgentAdminPrompt
{
    Task<bool> ShowAsync(CancellationToken ct = default);
}
