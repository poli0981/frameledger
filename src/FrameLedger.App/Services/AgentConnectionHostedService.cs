using Microsoft.Extensions.Hosting;

namespace FrameLedger.App.Services;

/// <summary>Runs <see cref="AgentConnection"/> for the life of the host.</summary>
internal sealed class AgentConnectionHostedService(AgentConnection connection) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => connection.RunAsync(stoppingToken);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await connection.DisposeAsync().ConfigureAwait(false);
    }
}
