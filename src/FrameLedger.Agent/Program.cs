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
// One process per data folder runs the capturing verbs (--serve, capture, launch, recover): Infrastructure.Startup's
// AgentInstanceLock, claimed before logging, and a second exits 10 (2026-09-17 — beta.1 ran four Agents at once).
//
// The maintenance flags (P3 PR-8b) — --register-vklayer, --unregister-vklayer, --install-task, --uninstall-task —
// run over the product directory and exit; --diag is the App's (10_LOGGING) and answers "not implemented", exit 2.

using FrameLedger.Agent.Cli;
using FrameLedger.Agent.Composition;
using FrameLedger.Agent.Hosting;
using FrameLedger.Application.Ipc;
using FrameLedger.Application.Rules;
using FrameLedger.Infrastructure.Diagnostics;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Infrastructure.Rules;
using FrameLedger.Infrastructure.Startup;
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
    private const int _exitLedgerRefused = 7;

    private static int _crashReported;

    private static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        AgentCommandLine cmd = AgentCommandLine.Parse(args);
        if (AnswerWithoutLogging(cmd) is { } answered)
        {
            return answered;
        }

        AgentPaths paths = cmd.DataDirectory is { } dir ? new AgentPaths(Path.GetFullPath(dir)) : AgentPaths.Default;
        using AgentInstanceLock? claim = ClaimDataDirectory(cmd.Verb, paths, out bool heldElsewhere);
        if (heldElsewhere)
        {
            return AgentInstanceLock.ExitHeldElsewhere;
        }

        ConfigureLogging(paths);
        HookCrashHandlers(paths);

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

            LedgerDatabase? db = await OpenLedgerOrSayWhyAsync(cmd.Verb, paths).ConfigureAwait(false);
            if (db is null)
            {
                return cmd.Verb == AgentVerb.DbPath ? _exitOk : _exitLedgerRefused;
            }

            await using (db.ConfigureAwait(false))
            {
                return cmd.Verb switch
                {
                    AgentVerb.Serve => await ServeAsync(db, paths).ConfigureAwait(false),
                    AgentVerb.RegisterVkLayer or AgentVerb.UnregisterVkLayer or AgentVerb.InstallTask or AgentVerb.UninstallTask =>
                        await new MaintenanceVerbs(db, paths).RunAsync(cmd).ConfigureAwait(false),
                    _ => await ConsoleAsync(cmd, db, paths).ConfigureAwait(false),
                };
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The same report as another thread's, while the logger is still open; the exception stays unhandled, so the
            // exit is the runtime's, never one of this binary's own codes.
            ReportCrash(ex, paths, "Main");
            throw;
        }
        finally
        {
            await Log.CloseAndFlushAsync().ConfigureAwait(false);
        }
    }

    /// <summary>A usage error or a flag this binary does not implement is answered before any file is touched.</summary>
    private static int? AnswerWithoutLogging(AgentCommandLine cmd)
    {
        if (cmd.Verb == AgentVerb.WriteCrashDump)
        {
            // Before logging, rules and the ledger: the crashed parent still holds the day's log file, and a helper that
            // waited on anything the parent holds would be the deadlock it exists to avoid.
            return ParentDump.Run(cmd.CrashDumpFile!, AgentConsole.Problem);
        }

        if (cmd.Error is not null)
        {
            AgentConsole.Problem(cmd.Error);
            return _exitUsage;
        }

        if (cmd.Verb == AgentVerb.NotImplemented)
        {
            AgentConsole.Problem($"{cmd.Flag}: not this binary's — FrameLedger.exe --diag writes the report (10_LOGGING §Diagnostics extras)");
            return _exitNotImplemented;
        }

        return null;
    }

    /// <summary>
    /// The verbs that only print open the ledger read-only — no migration, no write — so that running one against a
    /// ledger at another schema changes nothing on disk (2026-09-16: <c>sessions</c> against the owner's own ledger
    /// applied two scripts to it). Every other verb is a writer and opens read-write, migrating as before.
    /// </summary>
    internal static bool PrintsOnly(AgentVerb verb) =>
        verb is AgentVerb.Sessions or AgentVerb.ConsentList or AgentVerb.KillSwitchStatus or AgentVerb.DbPath;

    /// <summary>
    /// The verbs that inject, read a ring or finalize sessions: one process per data folder may run them (2026-09-17). The
    /// others (consent, games, the kill switch, the maintenance flags, the read-only verbs) run beside a serving Agent, as
    /// the App's own <c>--install-task</c> does.
    /// </summary>
    internal static bool Captures(AgentVerb verb) =>
        verb is AgentVerb.Serve or AgentVerb.Capture or AgentVerb.Launch or AgentVerb.Recover;

    private static AgentInstanceLock? ClaimDataDirectory(AgentVerb verb, AgentPaths paths, out bool heldElsewhere)
    {
        heldElsewhere = false;
        if (!Captures(verb))
        {
            return null;
        }

        AgentInstanceLock? claim = AgentInstanceLock.TryAcquire(paths.DataDirectory);
        if (claim is null)
        {
            // Before logging, rules and the ledger: the Agent holding the folder holds the day's log file too, so a second one
            // leaves nothing behind but this line and its exit code, which the App's log records when the App started it.
            heldElsewhere = true;
            AgentConsole.Problem($"another FrameLedger Agent is already capturing for {paths.DataDirectory}; this one exits (exit {AgentInstanceLock.ExitHeldElsewhere})");
        }

        return claim;
    }

    /// <summary>
    /// Null when a print verb refuses the ledger — at another schema, or not there — said on the console and in the log
    /// rather than as a crash: nothing about this machine is broken.
    /// </summary>
    private static async Task<LedgerDatabase?> OpenLedgerOrSayWhyAsync(AgentVerb verb, AgentPaths paths)
    {
        if (verb == AgentVerb.DbPath)
        {
            // Prints a path; opens nothing. A machine with no ledger yet still has an answer, and the verb never reaches
            // ConsoleVerbs because the answer is complete here.
            AgentConsole.Line(paths.Database);
            return null;
        }

        try
        {
            LedgerDatabase db = PrintsOnly(verb)
                ? await LedgerDatabase.OpenReadOnlyAsync(paths.Database).ConfigureAwait(false)
                : await LedgerDatabase.OpenAsync(paths.Database, diagnostics: static line => Log.Information("{Line}", line)).ConfigureAwait(false);
            Log.Information("ledger: {Path} (schema {Schema}, migration {Migration}{ReadOnly})", db.Path, db.SchemaVersion, db.Migration,
                db.IsReadOnly ? ", read-only" : string.Empty);
            return db;
        }
        catch (Exception ex) when (ex is LedgerSchemaException or FileNotFoundException)
        {
            AgentConsole.Problem(ex.Message);
            Log.Warning("ledger: {Message}", ex.Message);
            return null;
        }
    }

    private static void ConfigureLogging(AgentPaths paths)
    {
        Directory.CreateDirectory(paths.Logs);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(paths.Logs, "agent-.log"), formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();
    }

    /// <summary>
    /// <c>10_LOGGING</c> §Crash handling, in both processes (P4 PR-9): Fatal with the whole exception, then a minidump under
    /// <c>crashdumps\</c>. The Agent has no window, so there is no dialog; the App's bug report offers the dump.
    /// </summary>
    private static void HookCrashHandlers(AgentPaths paths)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            ReportCrash(e.ExceptionObject as Exception, paths, "AppDomain");
            Log.CloseAndFlush();
        };
        TaskScheduler.UnobservedTaskException += static (_, e) => Log.Error(e.Exception, "agent: unobserved task exception");
    }

    /// <summary><c>10_LOGGING</c> §Crash handling: Fatal, then the minidump; once per process, whichever path sees the exception first.</summary>
    private static void ReportCrash(Exception? exception, AgentPaths paths, string source)
    {
        if (Interlocked.Exchange(ref _crashReported, 1) == 1)
        {
            return;
        }

        Log.Fatal(exception, "agent: unhandled exception ({Source}); the Agent exits", source);
        string? dump = CrashDumpWriter.TryWrite(paths.CrashDumps, "agent", DateTimeOffset.UtcNow, Environment.ProcessPath, static line => Log.Warning("{Line}", line));
        Log.Information("agent: crash dump {Dump}", dump ?? "not written");
    }

    /// <summary>The product's mode: a Generic Host around the watcher, stopped by Ctrl+C or the service manager.</summary>
    private static async Task<int> ServeAsync(LedgerDatabase db, AgentPaths paths)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        // Before the composition, so its TryAdd keeps this one: Shutdown over the pipe stops the host (PR-1b).
        builder.Services.AddSingleton<IAgentLifetime, HostAgentLifetime>();
        builder.Services.AddFrameLedgerAgent(db, paths);
        builder.Services.AddHostedService<WatcherHostedService>();
        // The static detection sweep (P4 PR-1, 05_DETECTION §Caching): its own task, because the probe blocks on file I/O.
        builder.Services.AddHostedService<DetectionHostedService>();
        // The pipe (07_IPC §C), the product's UI channel: --serve only, never under --console (P3 PR-1).
        builder.Services.AddHostedService<PipeServerHostedService>();
        // Finalize's grace on shutdown (04_CAPTURE §Threading model): a session that does not make it leaves
        // its .partial for the next start's recovery.
        builder.Services.Configure<HostOptions>(static o => o.ShutdownTimeout = TimeSpan.FromSeconds(15));

        using IHost host = builder.Build();
        AgentConsole.Line($@"serve: ledger {paths.Database}; logs {paths.Logs}; pipe \.\pipe\{Shared.Ipc.IpcProtocol.PipeName}; Ctrl+C stops");
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
