// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Services;

/// <summary>The exit code and the combined output of one Agent maintenance run; a negative exit code means it did not run.</summary>
public sealed record AgentToolResult(int ExitCode, string Output)
{
    public bool Succeeded => ExitCode == 0;
}
