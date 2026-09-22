using FrameLedger.Application.Detection;
using FrameLedger.Application.Vulkan;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace FrameLedger.Agent.Hosting;

/// <summary>
/// <c>--serve</c> (P4 PR-1): the static detection sweep on its own background task. One pass at start, then one
/// every <see cref="Interval"/>, and one as soon as <c>UpdateRules</c> re-seeds the rules file
/// (<see cref="DetectionSweep.RequestNow"/>). It reads game files and writes the detected columns of <c>games</c>;
/// it never touches a process, a hook, or a consent column (<c>05_DETECTION</c> §Caching, CLAUDE.md rule 4).
/// </summary>
/// <remarks>
/// Its own task because <c>GameFileProbe</c> blocks on file I/O (a bounded directory walk, an 8 MB strings pass):
/// the orchestrator's 1 Hz poll must not wait on it, and a session's drain must never share a thread with it.
/// A sweep that throws is logged and retried on the next tick rather than ending the service — a game's
/// directory going away mid-walk is ordinary, and one bad game must not stop the scan of the rest.
/// </remarks>
internal sealed class DetectionHostedService(DetectionSweep sweep, VkLayerReconciler layer) : BackgroundService
{
    /// <summary>The idle cadence. A game the App just added is seen within this; a rules update wakes the loop at once.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("detect: sweeping the library every {Seconds} s, and on every rules update", Interval.TotalSeconds);
        DetectionSweepReport? previous = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                DetectionSweepReport r = await sweep.SweepOnceAsync(stoppingToken).ConfigureAwait(false);
                // Said when something happened or changed (2026-09-23): an unreadable entry used to repeat this line every
                // 15 s — 5,760 a day — with nothing new in it.
                if (r.Scanned > 0 || r.Relocated > 0 || r.RulesUnusable || (r.Unreadable > 0 && r.Unreadable != previous?.Unreadable))
                {
                    Log.Information("detect: sweep scanned {Scanned}, current {Current}, unreadable {Unreadable}{Rules}",
                        r.Scanned, r.Current, r.Unreadable, r.RulesUnusable ? ", rules unusable" : string.Empty);
                }

                previous = r;

                // The layer follows the ledger after every sweep (P4 PR-2): a Vulkan fact that arrived after the
                // consent, and the crash policy's auto-disable, both converge here within one interval.
                _ = await layer.ReconcileAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                Log.Warning(ex, "detect: sweep did not complete; retrying on the next tick");
            }

            try
            {
                await sweep.WaitAsync(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        Log.Information("detect: stopped");
    }
}
