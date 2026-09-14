using System.Reflection;
using FrameLedger.Application.Ipc;
using FrameLedger.Infrastructure.AntiCheat;
using FrameLedger.Infrastructure.Vulkan;
using FrameLedger.Shared.Safety;

namespace FrameLedger.Agent.Composition;

/// <summary>What <c>HelloAck</c> says about this process, read once at composition.</summary>
internal static class AgentIdentityFactory
{
    public static AgentIdentity OfThisProcess(string vkLayerDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vkLayerDirectory);
        string buildId = NativeAntiCheatGuard.BuildId();
        return new AgentIdentity(
            AgentVersion: Version(),
            Pid: Environment.ProcessId,
            Elevated: Environment.IsPrivilegedProcess,
            OverlayBuildId: buildId.Length == 0 ? null : buildId,
            // The layer reaches a launched process through VK_ADD_IMPLICIT_LAYER_PATH; the HKCU registration is the
            // repair tool --register-vklayer writes (P3 PR-8b), read here as it stands at this process's start.
            VulkanLayerRegistered: new VkLayerRegistration().IsRegistered(Path.Combine(vkLayerDirectory, VkLayerLaunchEnvironment.ManifestFileName)),
            // AgentRecording.Poller composes LhmComputerAdapter(enableCpuAndMemory: false): no CPU sensor unelevated.
            CpuTempAvailable: false,
            // D14: the FR-2.1 text this build stamps against; the App compares it with its own before showing the dialog.
            DisclosureVersion: SafetyDisclosure.Version);
    }

    private static string Version()
    {
        Assembly assembly = typeof(AgentIdentityFactory).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "0.0.0";
    }
}
