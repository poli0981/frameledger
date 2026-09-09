using FrameLedger.Application.AntiCheat;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Consent;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Telemetry;
using FrameLedger.Infrastructure.Telemetry;

namespace FrameLedger.Agent.Composition;

/// <summary>
/// The recorder as the Agent composes it (P2 PR-F): every collaborator is the DI singleton, and what varies
/// per request — the bound on a console capture, the launcher with the Vulkan layer's environment — is a
/// parameter here rather than a second registration. The unshipped host does the same by hand in
/// <c>HostRecorder</c>; this is that composition with the container holding the adapters.
/// </summary>
internal sealed class AgentRecording
{
    private readonly IGameConsentStore _store;
    private readonly HookedCaptureGate _gate;
    private readonly IAntiCheatGuard _guard;
    private readonly ITargetResolver _resolver;
    private readonly ITargetLivenessSource _liveness;
    private readonly IRingAttacher _attacher;
    private readonly IRuntimeModuleSnapshot _modules;
    private readonly INgxDriverProbe _ngx;
    private readonly IKillSwitch _killSwitch;
    private readonly IGameRepository _games;
    private readonly IHardwareSnapshotRepository _snapshots;
    private readonly IHardwareSnapshotSource _hardware;
    private readonly IPartialSessionStore _partials;
    private readonly SessionFinalizer _finalizer;
    private readonly ICrashEventSource _crashes;

    public AgentRecording(
        IGameConsentStore store,
        HookedCaptureGate gate,
        IAntiCheatGuard guard,
        ITargetResolver resolver,
        ITargetLivenessSource liveness,
        IRingAttacher attacher,
        IRuntimeModuleSnapshot modules,
        INgxDriverProbe ngx,
        IKillSwitch killSwitch,
        IGameRepository games,
        IHardwareSnapshotRepository snapshots,
        IHardwareSnapshotSource hardware,
        IPartialSessionStore partials,
        SessionFinalizer finalizer,
        ICrashEventSource crashes)
    {
        _store = store;
        _gate = gate;
        _guard = guard;
        _resolver = resolver;
        _liveness = liveness;
        _attacher = attacher;
        _modules = modules;
        _ngx = ngx;
        _killSwitch = killSwitch;
        _games = games;
        _snapshots = snapshots;
        _hardware = hardware;
        _partials = partials;
        _finalizer = finalizer;
        _crashes = crashes;
    }

    /// <summary>
    /// A recorder for one request. <paramref name="seconds"/> bounds the session (0 = until the target exits) and,
    /// for a bounded operator capture, lowers the discard threshold to the bound — an operator who asked for 8 s
    /// wants the 8 s row, and the product's 30 s minimum is for sessions nobody bounded. The Agent keeps the
    /// product's 60 s <c>.partial</c> flush.
    /// </summary>
    public SessionRecorder Recorder(int seconds, IProcessLauncher? launcher)
    {
        TimeSpan minimum = seconds > 0
            ? TimeSpan.FromSeconds(Math.Min(seconds, (int)SessionFinalizer.MinimumSessionLength.TotalSeconds))
            : SessionFinalizer.MinimumSessionLength;
        return new SessionRecorder(
            new Factory(this, seconds, launcher),
            _games,
            _snapshots,
            _hardware,
            _partials,
            _finalizer,
            _crashes,
            Poller,
            TimeProvider.System,
            new RecorderOptions { MinimumSessionLength = minimum });
    }

    /// <summary>L1 + L2 + L3 under the composite, one poller per session; the poller owns and disposes the layers.</summary>
    public static TelemetryPoller Poller()
    {
        // Ownership walks up one step at a time and each local is nulled at its hand-off, so a throw
        // anywhere below disposes exactly what nobody owns yet.
        PdhAdapterMemoryCounter? pdh = null;
        BaselineTelemetrySource? l1 = null;
        LhmTelemetrySource? l2 = null;
        NvapiTelemetrySource? l3 = null;
        CompositeTelemetrySource? composite = null;
        try
        {
            pdh = new PdhAdapterMemoryCounter();
            l1 = new BaselineTelemetrySource(new DxgiAdapters(), pdh, TimeProvider.System);
            pdh = null;
            l2 = new LhmTelemetrySource(new LhmComputerAdapter(enableCpuAndMemory: false), new LhmTelemetryOptions(), TimeProvider.System);
            l3 = new NvapiTelemetrySource(TimeProvider.System);
            l1.Start();
            l2.Start();
            l3.Start();
            composite = new CompositeTelemetrySource([l1, l2, l3]);
            l1 = null;
            l2 = null;
            l3 = null;
            var poller = new TelemetryPoller(composite, new TelemetryPollerOptions(), TimeProvider.System, ownsSource: true);
            composite = null;
            return poller;
        }
        finally
        {
            pdh?.Dispose();
            l1?.Dispose();
            l2?.Dispose();
            l3?.Dispose();
            composite?.Dispose();
        }
    }

    private CaptureSession Session(int seconds, IProcessLauncher? launcher, ICaptureObserver observer) =>
        new(
            _store,
            _gate,
            _guard,
            _resolver,
            _liveness,
            _attacher,
            new CaptureOptions
            {
                // Zero keeps the product behaviour: run until the target exits. A positive --seconds is an
                // operator taking a bounded measurement.
                MaxDuration = seconds > 0 ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero,
            },
            _modules,
            _ngx,
            launcher,
            observer,
            _killSwitch);

    /// <summary>The session and its collaborators, wired the only way the Agent allows; the recorder supplies the observer.</summary>
    private sealed class Factory(AgentRecording owner, int seconds, IProcessLauncher? launcher) : ICaptureSessionFactory
    {
        public CaptureSession Create(ICaptureObserver observer) => owner.Session(seconds, launcher, observer);
    }
}
