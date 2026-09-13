using FrameLedger.Application.Capture;
using FrameLedger.Application.Recording;
using FrameLedger.Application.Watch;
using FrameLedger.Infrastructure.Capture;
using FrameLedger.Infrastructure.Vulkan;

namespace FrameLedger.Agent.Composition;

/// <summary>
/// Launch mode for the pipe's <c>LaunchGame</c> (P3 PR-1b): the same environment the console's <c>launch</c>
/// verb builds — the Vulkan layer's variables unless FR-2.4's switch is on (decision D7: the switch is honoured
/// by NOT setting the enable variable) — and a recorder over it, unbounded, with the product's minimum length.
/// </summary>
internal sealed class AgentLaunches(AgentRecording recording, IKillSwitch killSwitch, AgentPaths paths) : ILaunchRecorderFactory
{
    public async ValueTask<ILaunchRecording> PrepareAsync(string normalisedExePath, CancellationToken ct = default)
    {
        bool engaged = await killSwitch.IsEngagedAsync(ct).ConfigureAwait(false);
        VkLayerLaunchEnvironment? vulkan = engaged ? null : VkLayerLaunchEnvironment.Prepare(normalisedExePath, AgentPaths.VkLayerDll, paths.VkLayerDirectory);
        string description = engaged
            ? "vulkan layer: NOT enabled - the global kill switch is on (FR-2.4), so the launched process gets no layer environment"
            : vulkan!.ManifestPath is null
                ? "vulkan layer: NOT staged beside this binary - a Vulkan title will present unobserved"
                : $"vulkan layer: {vulkan.ManifestPath} (via {VkLayerLaunchEnvironment.ImplicitLayerPathVariable}; enable-list entry '{vulkan.ImageName}' for this session)";
        var launcher = new ProcessLauncher(vulkan?.Variables, enableVulkanLayer: !engaged);
        return new Recording(recording.Recorder(seconds: 0, launcher), vulkan, description);
    }

    private sealed class Recording(ISessionRecorder recorder, VkLayerLaunchEnvironment? vulkan, string description) : ILaunchRecording
    {
        public ISessionRecorder Recorder { get; } = recorder;

        public string Description { get; } = description;

        public void Dispose() => vulkan?.Dispose();
    }
}
