using System.Data.Common;
using FrameLedger.Application.AntiCheat;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace FrameLedger.Agent.Hosting;

/// <summary>
/// <c>--serve</c> (beta.8, 2026-09-25): the anti-cheat pre-scan of the library on its own background task. One pass at
/// start, then one every <see cref="Interval"/>, and one as soon as <c>UpdateRules</c> re-seeds the rules file
/// (<see cref="AntiCheatPreScanSweep.RequestNow"/>). It reads executables' paths, install folders and the stores' own
/// files, and writes the pre-scan's hook-state columns of <c>games</c>; it never opens a game's process
/// (<c>19_SAFETY</c> §What a finding does to the game, CLAUDE.md rule 4).
/// </summary>
/// <remarks>
/// Its own task, like <see cref="DetectionHostedService"/>'s, because the guard's directory walk blocks on file I/O.
/// A pass that throws is logged and retried on the next tick rather than ending the service: a game's folder going away
/// mid-walk is ordinary, a busy ledger is a retry, and a service that throws stops the Agent's host.
/// </remarks>
internal sealed class AntiCheatPreScanHostedService(AntiCheatPreScanSweep sweep) : BackgroundService
{
    /// <summary>The idle cadence, the detection sweep's: a game the App just added is scanned within it.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("prescan: scanning the library for anti-cheat every {Seconds} s, and on every rules update", Interval.TotalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                AntiCheatPreScanReport r = await sweep.SweepOnceAsync(stoppingToken).ConfigureAwait(false);
                // Said when something happened: a pass over an unchanged library scans nothing and says nothing.
                if (r.Scanned > 0 || r.RulesUnusable)
                {
                    Log.Information("prescan: scanned {Scanned} — anti-cheat {Found}, clean {Clean}, could not verify {Unverified}; blocked {Blocked}, current {Current}{Rules}",
                        r.Scanned, r.Found, r.Clean, r.Unverified, r.Blocked, r.Current, r.RulesUnusable ? ", rules unusable" : string.Empty);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or DbException)
            {
                Log.Warning(ex, "prescan: pass did not complete; retrying on the next tick");
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

        Log.Information("prescan: stopped");
    }
}
