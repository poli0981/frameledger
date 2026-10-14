// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using Microsoft.Extensions.Hosting;

namespace FrameLedger.App.Services;

/// <summary>Runs <see cref="AgentConnection"/> for the life of the host.</summary>
internal sealed class AgentConnectionHostedService(AgentConnection connection, LiveSessions sessions, SessionNotices notices) : BackgroundService
{
    // The running-session view is built with this service, before the first round, so the first connect's status seeds
    // it and no event after it is missed (2026-09-23); so is FR-3.8's notice (beta.23), on the UI thread the host starts on.
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        GC.KeepAlive(sessions);
        GC.KeepAlive(notices);
        return connection.RunAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await connection.DisposeAsync().ConfigureAwait(false);
    }
}
