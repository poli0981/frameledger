using FrameLedger.Infrastructure.Ipc;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace FrameLedger.Agent.Hosting;

/// <summary>
/// The pipe under <c>--serve</c> (P3 PR-1): listen until the host stops. <c>--console</c> never starts this, so
/// the same composition publishes to nobody there — the publisher checks for clients, not for a mode.
/// </summary>
internal sealed class PipeServerHostedService(PipeServer server) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("pipe: listening on \\\\.\\pipe\\{Name} with DACL {Sddl}", server.PipeName, server.SecurityDescriptor);
        try
        {
            await server.RunAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        Log.Information("pipe: stopped ({Rejected} refused connection(s), {Dropped} dropped frame(s))", server.RejectedClients, server.DroppedFrames);
    }
}
