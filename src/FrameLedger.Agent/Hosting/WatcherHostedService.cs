using FrameLedger.Application.Recording;
using FrameLedger.Application.Watch;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace FrameLedger.Agent.Hosting;

/// <summary>
/// <c>--serve</c> (P2 PR-F): recover what a previous Agent left behind, then watch. The pipe (<c>07_IPC</c>
/// §C) is P3; until then this is the whole background Agent, and it logs where a UI would listen.
/// </summary>
/// <remarks>
/// <b>Recovery runs first, every start</b> (<c>04_CAPTURE</c> §Session recorder): a pending <c>.partial</c> is
/// a session somebody lost, and it becomes an <c>interrupted</c> row before any new session can write beside
/// it. Then the orchestrator polls until the host stops; its sessions finalize inside the host's shutdown
/// grace, and one that does not make it leaves its <c>.partial</c> for the next start — the same rule.
/// </remarks>
internal sealed class WatcherHostedService(PartialRecovery recovery, CaptureOrchestrator orchestrator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<RecoveryOutcome> recovered = await recovery.RecoverAsync(stoppingToken).ConfigureAwait(false);
        Log.Information("recover: {Count} pending .partial file(s)", recovered.Count);
        foreach (RecoveryOutcome o in recovered)
        {
            Log.Information("recover: {Guid} {Status} {Detail}", o.SessionGuid.ToString("N"), o.Status, o.Detail);
        }

        Log.Information("serve: watching the games table at 1 Hz; hooking only where a game is enabled and consented");
        await orchestrator.RunAsync(stoppingToken).ConfigureAwait(false);
        Log.Information("serve: stopped");
    }
}
