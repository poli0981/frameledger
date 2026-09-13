using FrameLedger.Agent.Hosting;
using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Ipc;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Rules;
using FrameLedger.Application.Settings;
using FrameLedger.Application.Watch;
using FrameLedger.Infrastructure.AntiCheat;
using FrameLedger.Infrastructure.Blobs;
using FrameLedger.Infrastructure.Capture;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Infrastructure.Recording;
using FrameLedger.Infrastructure.Rules;
using FrameLedger.Infrastructure.Settings;
using FrameLedger.Infrastructure.Telemetry;
using FrameLedger.Infrastructure.Watch;
using FrameLedger.Shared.Ipc;
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

        AddPipe(services, pipeName);

        return services;
    }

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
            static async ct => (await new RulesSeeder(new FileSystemRulesStore()).EnsureSeededAsync(ct).ConfigureAwait(false)).ToString()));
        services.AddSingleton<IIpcRequestHandler>(static sp => new AgentRequestHandler(
            AgentIdentityFactory.OfThisProcess(),
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
            sp.GetRequiredService<ILaunchRecorderFactory>()));
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
