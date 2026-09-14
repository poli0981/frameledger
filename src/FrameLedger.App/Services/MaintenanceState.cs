using System.IO;
using FrameLedger.Infrastructure.Startup;
using FrameLedger.Infrastructure.Vulkan;

namespace FrameLedger.App.Services;

/// <summary><see cref="IMaintenanceState"/> over the same registry key and task name the Agent's flags write.</summary>
public sealed class MaintenanceState : IMaintenanceState
{
    private static string ManifestPath => Path.Combine(VkLayerLaunchEnvironment.DefaultDirectory, VkLayerLaunchEnvironment.ManifestFileName);

    private static string LayerDll => Path.Combine(AppContext.BaseDirectory, "FrameLedger.VkLayer.dll");

    public async Task<MaintenanceSnapshot> ReadAsync(CancellationToken ct = default)
    {
        bool staged = File.Exists(LayerDll);
        bool registered = new VkLayerRegistration().IsRegistered(ManifestPath);
        // The Task Scheduler COM call is synchronous; off the caller's thread.
        LogonTaskState task = File.Exists(AgentTool.AgentPath)
            ? await Task.Run(static () => new LogonTask().Query(AgentTool.AgentPath), ct).ConfigureAwait(false)
            : LogonTaskState.Unknown;
        return new MaintenanceSnapshot(staged, registered, task);
    }
}
