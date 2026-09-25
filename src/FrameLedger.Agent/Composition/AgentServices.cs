using FrameLedger.Agent.Hosting;
using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Detection;
using FrameLedger.Application.Ipc;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Rules;
using FrameLedger.Application.Settings;
using FrameLedger.Application.Vulkan;
using FrameLedger.Application.Watch;
using FrameLedger.Infrastructure.AntiCheat;
using FrameLedger.Infrastructure.Blobs;
using FrameLedger.Infrastructure.Capture;
using FrameLedger.Infrastructure.Detection;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Infrastructure.Recording;
using FrameLedger.Infrastructure.Rules;
using FrameLedger.Infrastructure.Settings;
using FrameLedger.Infrastructure.Telemetry;
using FrameLedger.Infrastructure.Vulkan;
using FrameLedger.Infrastructure.Watch;
using FrameLedger.Shared.Ipc;
using FrameLedger.Shared.Safety;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FrameLedger.Agent.Composition;

/// <summary>
/// The Agent's composition root (P2 PR-F): every <c>Application</c> port to its one shipped
/// <c>Infrastructure</c> adapter, once. What <c>01_ARCHITECTURE</c> §Component model calls the Agent is this
/// list plus <c>WatcherHostedService</c>; nothing here decides anything about hooking.
/// </summary>
/// <remarks>
/// <para>
/// <b>The guard's chokepoint is the same object under DI as by hand.</b> <see cref="HookedCaptureGate"/> over
/// <see cref="NativeAntiCheatGuard"/>, reached only by <c>CaptureSession</c> through
/// <c>HookRequest.FromConsent</c>; the container adds no second route. <see cref="IKillSwitch"/> is the
/// gate's fourth input (decision D7), read from <c>settings</c>.
/// </para>
/// <para>
/// <b>Singletons, because the process is the scope.</b> One ledger connection (opened before the container,
/// handed in), one NGX probe holding one <c>NvAPI_Initialize</c> for the Agent's life, one
/// <c>tmp\</c> for the <c>.partial</c> files. The container disposes what it created.
/// </para>
/// </remarks>
internal static class AgentServices
{
    // pipeName: a test names its own pipe so it never answers a real UI; the product uses the default.
    public static IServiceCollection AddFrameLedgerAgent(this IServiceCollection services, LedgerDatabase db, AgentPaths paths, string? pipeName = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(paths);

        services.AddSingleton(db);
        services.AddSingleton(paths);

        // Persistence: the one ledger, the Agent's own (%LOCALAPPDATA%\FrameLedger, §S18 blocker 3).
        services.AddSingleton<IGameConsentStore, SqliteGameConsentStore>();
        services.AddSingleton<IGameRepository, SqliteGameRepository>();
        services.AddSingleton<ISessionRepository, SqliteSessionRepository>();
        services.AddSingleton<IHardwareSnapshotRepository, SqliteHardwareSnapshotRepository>();
        services.AddSingleton<ISettingsStore, SqliteSettingsStore>();
        // The registry over the store (P3 PR-3, D16): the recorder resolves its three keys at each session start.
        services.AddSingleton<RegisteredSettings>();
        services.AddSingleton<IRecorderPolicy, SettingsRecorderPolicy>();
        services.AddSingleton<SettingsKillSwitch>();
        services.AddSingleton<IKillSwitch>(static sp => sp.GetRequiredService<SettingsKillSwitch>());

        // The gate and the guard: one native facade, one managed gate in front of it.
        services.AddSingleton<IAntiCheatGuard, NativeAntiCheatGuard>();
        services.AddSingleton<HookedCaptureGate>();

        // The capture path's adapters (PR-C). The resolver's notes reach the Agent's log too (2026-09-23: there were none in
        // it), and its Tier-2 liveness reads the watcher's snapshot by image path, never a process of its own.
        services.AddSingleton<LatestProcessSnapshot>();
        services.AddSingleton<ITargetResolver>(static sp => new TargetResolver(
            static line =>
            {
                AgentConsole.Line(line);
                Serilog.Log.Information("{Line}", line);
            },
            sp.GetRequiredService<LatestProcessSnapshot>()));
        services.AddSingleton<ITargetLivenessSource, HeldProcessLivenessSource>();
        services.AddSingleton<IRingAttacher>(static _ => new ShmRingAttacher(NativeAntiCheatGuard.BuildId()));
        services.AddSingleton<IRuntimeModuleSnapshot>(static _ => new RuntimeModuleSnapshot(CensusNames.ModuleFileNames));
        services.AddSingleton<NvapiNgxStateProbe>();
        services.AddSingleton<INgxDriverProbe>(static sp => sp.GetRequiredService<NvapiNgxStateProbe>());

        // The recorder's adapters (PR-D).
        services.AddSingleton<IPartialSessionStore>(_ => new PartialSessionStore(paths.Tmp));
        services.AddSingleton<ICrashEventSource, EventLogCrashSource>();
        services.AddSingleton<IHardwareSnapshotSource>(static _ => new HardwareSnapshotSource(new DxgiAdapters()));
        services.AddSingleton<ISeriesCodec, DeflateSeriesCodec>();
        services.AddSingleton<SessionFinalizer>();
        services.AddSingleton(static sp => new PartialRecovery(sp.GetRequiredService<IPartialSessionStore>(), sp.GetRequiredService<SessionFinalizer>()));
        services.AddSingleton<AgentRecording>();

        // The watcher (this PR): snapshots, identity, and the orchestrator over an unbounded recorder.
        services.AddSingleton<IProcessSnapshotSource, ToolhelpProcessSnapshotSource>();
        services.AddSingleton<IExecutableIdentitySource, ExecutableIdentitySource>();

        AddDetection(services, paths);
        services.AddSingleton<ISessionRecorder>(static sp => sp.GetRequiredService<AgentRecording>().Recorder(seconds: 0, launcher: null));

        AddPipe(services, pipeName);

        return services;
    }

    /// <summary>The static detection sweep (P4 PR-1, <c>05_DETECTION</c> §Caching): hosted under <c>--serve</c> only, composed and inert under <c>--console</c>.</summary>
    private static void AddDetection(IServiceCollection services, AgentPaths paths)
    {
        // The static detection sweep (P4 PR-1, 05_DETECTION §Caching): the rules file RulesSeeder seeds (the two
        // paths agree, RulesPathAgreementTests), the real probe, and the sweep over the games table. Hosted under
        // --serve only (DetectionHostedService); composed and inert under --console.
        services.AddSingleton<IDetectionRulesSource>(static _ => new DetectionRulesFile());
        services.AddSingleton<IGameFileProbe, GameFileProbe>();
        // A drive that changed its letter (2026-09-22): one relocator, asked by the sweep, the watcher and the enable-hooking
        // command, over the BCL's drive list. Since 2026-09-23 it also merges two entries for one executable (D26), and waits
        // while a session of either runs — the orchestrator's table, read when asked (it holds the relocator itself) — or
        // waits in a .partial to be recovered.
        services.AddSingleton<IGameMerge>(static sp => new SqliteGameMerge(sp.GetRequiredService<LedgerDatabase>()));
        services.AddSingleton(static sp => new ExecutableRelocator(
            sp.GetRequiredService<IGameRepository>(),
            sp.GetRequiredService<IExecutableIdentitySource>(),
            VolumeRoots.Ready,
            static line => Serilog.Log.Information("{Line}", line),
            merge: sp.GetRequiredService<IGameMerge>(),
            busy: id => sp.GetRequiredService<CaptureOrchestrator>().IsRecording(id)
                        || sp.GetRequiredService<IPartialSessionStore>().PendingGameIds().Contains(id)));
        services.AddSingleton(static sp => new DetectionSweep(
            sp.GetRequiredService<IGameRepository>(),
            sp.GetRequiredService<IDetectionRulesSource>(),
            sp.GetRequiredService<IGameFileProbe>(),
            sp.GetRequiredService<IExecutableIdentitySource>(),
            static line => Serilog.Log.Information("{Line}", line),
            sp.GetRequiredService<ExecutableRelocator>()));

        // The anti-cheat pre-scan of the library (beta.8, 19_SAFETY §What a finding does to the game): every entry through
        // the guard's advisory checks 3 and 4, again whenever the rules or the executable change. It waits while a session
        // of the entry runs (the session's own checks are running then), and tells the App only when it turned hooking off
        // for an entry the user had turned on. Hosted under --serve only, beside the detection sweep.
        services.AddSingleton(static sp => new AntiCheatPreScanSweep(
            sp.GetRequiredService<IGameRepository>(),
            sp.GetRequiredService<IGameConsentStore>(),
            sp.GetRequiredService<IAntiCheatGuard>(),
            sp.GetRequiredService<IDetectionRulesSource>(),
            sp.GetRequiredService<IExecutableIdentitySource>(),
            static line => Serilog.Log.Information("{Line}", line),
            sp.GetRequiredService<IIpcEventPublisher>(),
            id => sp.GetRequiredService<CaptureOrchestrator>().IsRecording(id)));

        // The layer's registration follows the ledger (P4 PR-2, 12_BUILD §The Vulkan layer is not registered at
        // install time): the same manifest and key --register-vklayer writes, reconciled after every consent change
        // the handler makes and after every sweep.
        // Inert off the profile ledger: a --data-dir Agent (tests, developers) never touches the user's HKCU.
        services.AddSingleton<IVkLayerRegistrar>(_ => Registrar(paths));
        services.AddSingleton(static sp => new VkLayerReconciler(
            sp.GetRequiredService<IGameConsentStore>(),
            sp.GetRequiredService<IGameRepository>(),
            sp.GetRequiredService<IVkLayerRegistrar>(),
            static line => Serilog.Log.Information("{Line}", line)));
    }

    /// <summary>The layer registrar for <paramref name="paths"/>: the real HKCU one over the profile ledger, inert anywhere else (P4 PR-2).</summary>
    internal static IVkLayerRegistrar Registrar(AgentPaths paths) =>
        paths.IsProfile ? new VkLayerRegistrar(paths.VkLayerDirectory, AgentPaths.VkLayerDll) : new InertVkLayerRegistrar();

    /// <summary>
    /// The pipe, read half (P3 PR-1, <c>07_IPC</c> §C, HANDOFF §P3 D11–D13). Events leave through <c>SessionEventPublisher</c>,
    /// the recorder's observer; requests are answered by <c>AgentRequestHandler</c>; the server itself is started by
    /// <c>PipeServerHostedService</c> under <c>--serve</c> only, so under <c>--console</c> this is composed and inert. The
    /// status the handler answers with is read lazily, which keeps the server → handler → publisher → server cycle out of
    /// construction.
    /// </summary>
    private static void AddPipe(IServiceCollection services, string? pipeName)
    {
        services.AddSingleton(new PipeServerOptions { PipeName = pipeName ?? IpcProtocol.PipeName });
        // The command half (PR-1b). The pause is the one global flag every session reads; the lifetime is the
        // host's under --serve and a logged no-op under --console (TryAdd: --serve registers its own first).
        services.AddSingleton<CapturePause>();
        services.TryAddSingleton<IAgentLifetime, NoAgentLifetime>();
        services.AddSingleton(static sp => new AgentCommandHandler(
            sp.GetRequiredService<IGameRepository>(),
            sp.GetRequiredService<IGameConsentStore>(),
            sp.GetRequiredService<IAntiCheatGuard>(),
            sp.GetRequiredService<IExecutableIdentitySource>(),
            sp.GetRequiredService<CaptureOrchestrator>(),
            sp.GetRequiredService<CapturePause>(),
            sp.GetRequiredService<IAgentLifetime>(),
            // UpdateRules: re-seed the rules file, then wake the detection sweep (P4 PR-1) — a rules change is the
            // re-run trigger 05_DETECTION §Caching names, so the games table follows within the same second.
            async ct =>
            {
                RulesSeedOutcome outcome = await new RulesSeeder(new FileSystemRulesStore()).EnsureSeededAsync(ct).ConfigureAwait(false);
                sp.GetRequiredService<DetectionSweep>().RequestNow();
                // And the anti-cheat pre-scan: new rules may name a game the library already holds (2026-09-25).
                sp.GetRequiredService<AntiCheatPreScanSweep>().RequestNow();
                return outcome.ToString();
            },
            // P3 PR-4 (D14): the reviewed disclosure this Agent stamps against, and its own clock for the stamp.
            SafetyDisclosure.Version,
            TimeProvider.System,
            sp.GetRequiredService<VkLayerReconciler>(),
            // P4 PR-7: the on-demand retention sweep, over the Agent's own settings read and its own blob tables.
            ct => new RetentionSweep(sp.GetRequiredService<ISessionRepository>(), sp.GetRequiredService<RegisteredSettings>()).RunAsync(ct),
            sp.GetRequiredService<ExecutableRelocator>()));
        services.AddSingleton<IIpcRequestHandler>(static sp => new AgentRequestHandler(
            AgentIdentityFactory.OfThisProcess(sp.GetRequiredService<AgentPaths>().VkLayerDirectory),
            TelemetryDescriptor(),
            () => sp.GetRequiredService<SessionEventPublisher>().Status,
            () => sp.GetRequiredService<AgentCommandHandler>()));
        services.AddSingleton(static sp => new PipeServer(
            sp.GetRequiredService<PipeServerOptions>(),
            sp.GetRequiredService<IIpcRequestHandler>(),
            static line => Serilog.Log.Information("{Line}", line)));
        services.AddSingleton<IIpcEventPublisher>(static sp => sp.GetRequiredService<PipeServer>());
        services.AddSingleton(static sp => new SessionEventPublisher(sp.GetRequiredService<IIpcEventPublisher>(), TimeProvider.System));
        services.AddSingleton<ISessionObserver>(static sp => sp.GetRequiredService<SessionEventPublisher>());
        services.AddSingleton(static sp => new CaptureOrchestrator(
            sp.GetRequiredService<ISessionRecorder>(),
            sp.GetRequiredService<IGameRepository>(),
            sp.GetRequiredService<IProcessSnapshotSource>(),
            sp.GetRequiredService<IExecutableIdentitySource>(),
            new OrchestratorOptions { PayloadPath = AgentPaths.Payload },
            static line => Serilog.Log.Information("{Line}", line),
            sp.GetRequiredService<ILaunchRecorderFactory>(),
            sp.GetRequiredService<ExecutableRelocator>(),
            sp.GetRequiredService<LatestProcessSnapshot>()));
        services.AddSingleton<ILaunchRecorderFactory, AgentLaunches>();

    }

    /// <summary>
    /// <c>HelloAck.telemetrySource</c>: the layers this machine composes, read once on the first <c>Hello</c> by
    /// standing the composite up and taking it down again — a library start, which is why it is not done at
    /// composition and not done per request.
    /// </summary>
    private static Func<string?> TelemetryDescriptor()
    {
        var descriptor = new Lazy<string?>(static () =>
        {
            using TelemetryPoller poller = AgentRecording.Poller(TimeSpan.FromSeconds(1));
            return poller.Descriptor;
        }, LazyThreadSafetyMode.ExecutionAndPublication);
        return () => descriptor.Value;
    }
}
