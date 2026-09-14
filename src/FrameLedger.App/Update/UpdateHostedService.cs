using FrameLedger.App.Services;
using FrameLedger.Infrastructure.Startup;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace FrameLedger.App.Update;

/// <summary>
/// The startup leg of <c>11_UPDATER</c> §Flow: the silent check 5 s after the host is up (the spec's "5 s after UI
/// idle", measured from start because the shell shows synchronously inside it), and — on the first run after an
/// update — the logon task's action path re-validated, because the updater rewrote the install directory
/// (<c>LogonTaskState.Stale</c> is the signal; <c>--install-task</c> the repair, this user's own task).
/// </summary>
internal sealed class UpdateHostedService(UpdateService updates, IMaintenanceState maintenance, IAgentTool tool) : BackgroundService
{
    private static readonly TimeSpan _startupDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (UpdateRestart.Detected)
            {
                await RepairTaskAsync(stoppingToken).ConfigureAwait(false);
            }

            await Task.Delay(_startupDelay, stoppingToken).ConfigureAwait(false);
            await updates.CheckSilentlyAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "update: the startup check faulted");
        }
    }

    private async Task RepairTaskAsync(CancellationToken ct)
    {
        MaintenanceSnapshot snapshot = await maintenance.ReadAsync(ct).ConfigureAwait(false);
        if (snapshot.Task != LogonTaskState.Stale)
        {
            Log.Information("update: restarted after an update; the logon task is {State}", snapshot.Task);
            return;
        }

        AgentToolResult result = await tool.RunAsync("--install-task", ct).ConfigureAwait(false);
        Log.Information("update: restarted after an update; the logon task was stale and --install-task exited {Exit}", result.ExitCode);
    }
}
