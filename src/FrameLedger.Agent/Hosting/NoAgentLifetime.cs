// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Ipc;
using Serilog;

namespace FrameLedger.Agent.Hosting;

/// <summary>Under <c>--console</c> there is no host to stop; the request is logged and nothing else happens.</summary>
internal sealed class NoAgentLifetime : IAgentLifetime
{
    public void RequestShutdown() => Log.Information("pipe: Shutdown requested, but this process hosts no service (--console); ignored");
}
