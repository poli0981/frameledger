using System.Reflection;
using FrameLedger.Application.Ipc;
using FrameLedger.Infrastructure.AntiCheat;

namespace FrameLedger.Agent.Composition;

/// <summary>What <c>HelloAck</c> says about this process, read once at composition.</summary>
internal static class AgentIdentityFactory
{
    public static AgentIdentity OfThisProcess()
    {
        string buildId = NativeAntiCheatGuard.BuildId();
        return new AgentIdentity(
            AgentVersion: Version(),
            Pid: Environment.ProcessId,
            Elevated: Environment.IsPrivilegedProcess,
            OverlayBuildId: buildId.Length == 0 ? null : buildId,
            // The layer reaches a launched process through VK_ADD_IMPLICIT_LAYER_PATH and is registered nowhere
            // (P1 item 3; --register-vklayer is P3 PR-8). False is the truth, not a placeholder.
            VulkanLayerRegistered: false,
            // AgentRecording.Poller composes LhmComputerAdapter(enableCpuAndMemory: false): no CPU sensor unelevated.
            CpuTempAvailable: false);
    }

    private static string Version()
    {
        Assembly assembly = typeof(AgentIdentityFactory).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "0.0.0";
    }
}
