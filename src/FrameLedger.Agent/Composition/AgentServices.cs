using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Watch;
using FrameLedger.Infrastructure.AntiCheat;
using FrameLedger.Infrastructure.Blobs;
using FrameLedger.Infrastructure.Capture;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Infrastructure.Recording;
using FrameLedger.Infrastructure.Settings;
using FrameLedger.Infrastructure.Telemetry;
using FrameLedger.Infrastructure.Watch;
using Microsoft.Extensions.DependencyInjection;

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
    public static IServiceCollection AddFrameLedgerAgent(this IServiceCollection services, LedgerDatabase db, AgentPaths paths)
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
        services.AddSingleton<SettingsKillSwitch>();
        services.AddSingleton<IKillSwitch>(static sp => sp.GetRequiredService<SettingsKillSwitch>());

        // The gate and the guard: one native facade, one managed gate in front of it.
        services.AddSingleton<IAntiCheatGuard, NativeAntiCheatGuard>();
        services.AddSingleton<HookedCaptureGate>();

        // The capture path's adapters (PR-C).
        services.AddSingleton<ITargetResolver>(static _ => new TargetResolver(AgentConsole.Line));
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
        services.AddSingleton<ISessionRecorder>(static sp => sp.GetRequiredService<AgentRecording>().Recorder(seconds: 0, launcher: null));
        services.AddSingleton(static sp => new CaptureOrchestrator(
            sp.GetRequiredService<ISessionRecorder>(),
            sp.GetRequiredService<IGameRepository>(),
            sp.GetRequiredService<IProcessSnapshotSource>(),
            sp.GetRequiredService<IExecutableIdentitySource>(),
            new OrchestratorOptions { PayloadPath = AgentPaths.Payload },
            static line => Serilog.Log.Information("{Line}", line)));

        return services;
    }
}
