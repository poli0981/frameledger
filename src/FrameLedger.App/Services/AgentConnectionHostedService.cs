using Microsoft.Extensions.Hosting;

namespace FrameLedger.App.Services;

/// <summary>Runs <see cref="AgentConnection"/> for the life of the host.</summary>
internal sealed class AgentConnectionHostedService(AgentConnection connection, LiveSessions sessions) : BackgroundService
{
    // The running-session view is built with this service, before the first round, so the first connect's status seeds
    // it and no event after it is missed (2026-09-23).
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        GC.KeepAlive(sessions);
        return connection.RunAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await connection.DisposeAsync().ConfigureAwait(false);
    }
}
