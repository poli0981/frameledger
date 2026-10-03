// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>Runs one of the Agent's maintenance flags (<c>--register-vklayer</c>, <c>--install-task</c>, …) as a child process and reports what it said.</summary>
public interface IAgentTool
{
    Task<AgentToolResult> RunAsync(string flag, CancellationToken ct = default);
}
