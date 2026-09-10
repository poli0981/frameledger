// FrameLedger.Agent — the capture orchestrator (P2 PR-F).
//
// The shipped host of the guard loop, by design (01_ARCHITECTURE §Component model; §S18 blocker 3):
// `--serve` watches the games table at 1 Hz and records a session per tracked process through
// SessionRecorder -> CaptureSession -> HookedCaptureGate -> FlGuardedInject; `--console` is the same
// composition driven by an operator's verb. Nothing here decides anything about hooking: per-game
// consent, the kill switch (FR-2.4) and every anti-cheat check are the gate's and the guard's.
//
// The Agent is the sole owner of %LOCALAPPDATA%\FrameLedger (§S18 blocker 3, ratified) and the only
// thing that may write there; `--data-dir` exists only under `--console` (HANDOFF §P2 decision D6).
//
// Flags 12_BUILD §Debugging lists that P2 does not build answer "not implemented", exit 2:
//   --diag --install-task --uninstall-task --register-vklayer --unregister-vklayer
// (--diag is the App's anyway, 10_LOGGING.)

using FrameLedger.Agent.Cli;
using FrameLedger.Agent.Composition;
using FrameLedger.Agent.Hosting;
using FrameLedger.Application.Rules;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Infrastructure.Rules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace FrameLedger.Agent;

internal static class Program
{
    private const int _exitOk = 0;
    private const int _exitUsage = 1;
    private const int _exitNotImplemented = 2;
    private const int _exitRulesFailed = 3;

    private static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        AgentCommandLine cmd = AgentCommandLine.Parse(args);
        if (cmd.Error is not null)
        {
            AgentConsole.Problem(cmd.Error);
            return _exitUsage;
        }

        if (cmd.Verb == AgentVerb.NotImplemented)
        {
            AgentConsole.Problem($"{cmd.Flag}: not implemented in P2 (12_BUILD §Debugging names it; HANDOFF §P2 scopes it out)");
            return _exitNotImplemented;
        }

        AgentPaths paths = cmd.DataDirectory is { } dir ? new AgentPaths(Path.GetFullPath(dir)) : AgentPaths.Default;
        Directory.CreateDirectory(paths.Logs);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(paths.Logs, "agent-.log"), formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();

        try
        {
            // Before anything else. The guard reads this file on every evaluation and refuses every title
            // when it is absent, so a capture started before the seed lands would fail for a reason that
            // has nothing to do with the game (§S20).
            RulesSeedOutcome seeded = await new RulesSeeder(new FileSystemRulesStore()).EnsureSeededAsync().ConfigureAwait(false);
            AgentConsole.Line($"rules: {Describe(seeded)}");
            Log.Information("rules: {Outcome}", seeded);
            if (seeded is RulesSeedOutcome.WriteFailed or RulesSeedOutcome.PackagedSeedUnusable)
            {
                return _exitRulesFailed;
            }

            LedgerDatabase db = await LedgerDatabase.OpenAsync(paths.Database).ConfigureAwait(false);
            await using (db.ConfigureAwait(false))
            {
                Log.Information("ledger: {Path} (schema {Schema}, migration {Migration})", db.Path, db.SchemaVersion, db.Migration);
                return cmd.Verb == AgentVerb.Serve
                    ? await ServeAsync(db, paths).ConfigureAwait(false)
                    : await ConsoleAsync(cmd, db, paths).ConfigureAwait(false);
            }
        }
        finally
        {
            await Log.CloseAndFlushAsync().ConfigureAwait(false);
        }
    }

    /// <summary>The product's mode: a Generic Host around the watcher, stopped by Ctrl+C or the service manager.</summary>
    private static async Task<int> ServeAsync(LedgerDatabase db, AgentPaths paths)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddFrameLedgerAgent(db, paths);
        builder.Services.AddHostedService<WatcherHostedService>();
        // Finalize's grace on shutdown (04_CAPTURE §Threading model): a session that does not make it leaves
        // its .partial for the next start's recovery.
        builder.Services.Configure<HostOptions>(static o => o.ShutdownTimeout = TimeSpan.FromSeconds(15));

        using IHost host = builder.Build();
        AgentConsole.Line($"serve: ledger {paths.Database}; logs {paths.Logs}; Ctrl+C stops");
        await host.RunAsync().ConfigureAwait(false);
        return _exitOk;
    }

    private static async Task<int> ConsoleAsync(AgentCommandLine cmd, LedgerDatabase db, AgentPaths paths)
    {
        ServiceProvider services = new ServiceCollection().AddFrameLedgerAgent(db, paths).BuildServiceProvider();
        await using (services.ConfigureAwait(false))
        {
            return await new ConsoleVerbs(services, paths).RunAsync(cmd).ConfigureAwait(false);
        }
    }

    private static string Describe(RulesSeedOutcome outcome) => outcome switch
    {
        RulesSeedOutcome.Installed => "installed the packaged blocklist",
        RulesSeedOutcome.Updated => "updated the blocklist this build ships",
        RulesSeedOutcome.AlreadyCurrent => "already current",
        RulesSeedOutcome.ForeignLeftAlone =>
            "a usable rules file is installed that we did not write — left alone (it can only ADD to the blocklist)",
        RulesSeedOutcome.ReplacedUnusable =>
            "the installed rules file was not usable by the guard and has been replaced",
        RulesSeedOutcome.RaceLost => "another process installed it first",
        RulesSeedOutcome.PackagedSeedUnusable =>
            "FAILED — the seed shipped in this build is not usable by the guard; nothing was installed",
        RulesSeedOutcome.WriteFailed => "FAILED — could not write the rules file",
        _ => outcome.ToString(),
    };
}
