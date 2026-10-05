// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using Serilog;

namespace FrameLedger.App.Services;

/// <summary>
/// The Agent's maintenance flags as a viewer sees them (beta.15, D52): the Vulkan layer's registration and the logon task
/// are this PC's, not a copy's, so nothing is started. The buttons are disabled; a call that reached here anyway fails.
/// </summary>
public sealed class ViewerAgentTool : IAgentTool
{
    /// <summary>The exit code a refused flag reports: not one the Agent itself uses.</summary>
    public const int ExitViewer = -2;

    public Task<AgentToolResult> RunAsync(string flag, CancellationToken ct = default)
    {
        Log.Warning("viewer: {Flag} is not run from a --data-dir window", flag);
        return Task.FromResult(new AgentToolResult(ExitViewer, Strings.Viewer_ChangesThisPc_Off));
    }
}
