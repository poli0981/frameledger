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
            // The same two conditions SystemTelemetrySource.Create opens the CPU reader under (2026-09-21): elevated AND
            // PawnIO installed. It was a constant false while nothing read a CPU sensor at all.
            CpuTempAvailable: Infrastructure.Telemetry.LhmEnvironment.IsElevated && Infrastructure.Telemetry.LhmEnvironment.IsPawnIoInstalled == true,
            // D14: the FR-2.1 text this build stamps against; the App compares it with its own before showing the dialog.
            DisclosureVersion: SafetyDisclosure.Version,
            // D33: the user-mode exception's disclosure, under the same rule.
            ExceptionDisclosureVersion: AntiCheatExceptionDisclosure.Version);
    }

    private static string Version()
    {
        Assembly assembly = typeof(AgentIdentityFactory).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "0.0.0";
    }
}
