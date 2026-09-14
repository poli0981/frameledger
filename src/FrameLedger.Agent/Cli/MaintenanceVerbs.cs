using FrameLedger.Domain.Consent;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Infrastructure.Startup;
using FrameLedger.Infrastructure.Vulkan;

namespace FrameLedger.Agent.Cli;

/// <summary>
/// The maintenance flags (P3 PR-8b), over the product directory, no options: the layer's HKCU registration and
/// the logon task. Both are repair tools that reflect state the product already holds — the registration exists
/// only while a game has hooking enabled (<c>12_BUILD</c> §The Vulkan layer is not registered at install time;
/// the ledger cannot yet say WHICH of them is a Vulkan title, so "any" is the check this build can make), and
/// the task starts the same <c>--serve</c> the App starts beside itself.
/// </summary>
internal sealed class MaintenanceVerbs(LedgerDatabase db, AgentPaths paths)
{
    private const int _exitOk = 0;
    private const int _exitUsage = 1;
    private const int _exitRefused = 2;

    public async Task<int> RunAsync(AgentCommandLine cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        return cmd.Verb switch
        {
            AgentVerb.RegisterVkLayer => await RegisterVkLayerAsync().ConfigureAwait(false),
            AgentVerb.UnregisterVkLayer => UnregisterVkLayer(),
            AgentVerb.InstallTask => await InstallTaskAsync().ConfigureAwait(false),
            AgentVerb.UninstallTask => await UninstallTaskAsync().ConfigureAwait(false),
            _ => _exitUsage,
        };
    }

    private string ManifestPath => Path.Combine(paths.VkLayerDirectory, VkLayerLaunchEnvironment.ManifestFileName);

    private async Task<int> RegisterVkLayerAsync()
    {
        if (!File.Exists(AgentPaths.VkLayerDll))
        {
            AgentConsole.Problem($"vklayer: {AgentPaths.VkLayerDll} is not beside this binary; nothing to register");
            return _exitUsage;
        }

        IReadOnlyList<GameConsentRecord> enabled = await new SqliteGameConsentStore(db).ListEnabledAsync().ConfigureAwait(false);
        if (enabled.Count == 0)
        {
            AgentConsole.Problem("vklayer: refused — no game has hooking enabled, and the layer is registered only while one does (12_BUILD §The Vulkan layer is not registered at install time)");
            return _exitRefused;
        }

        string manifest = VkLayerLaunchEnvironment.WriteManifest(paths.VkLayerDirectory, AgentPaths.VkLayerDll);
        new VkLayerRegistration().Register(manifest);
        AgentConsole.Line($"vklayer: registered {manifest} under HKCU\\{VkLayerRegistration.DefaultKeyPath} (this user; inert in any process without {VkLayerLaunchEnvironment.EnableVariable}=1)");
        return _exitOk;
    }

    private int UnregisterVkLayer()
    {
        bool removed = new VkLayerRegistration().Unregister(ManifestPath);
        AgentConsole.Line(removed ? $"vklayer: unregistered {ManifestPath}" : "vklayer: nothing was registered");
        return _exitOk;
    }

    private static Task<int> InstallTaskAsync()
    {
        string exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "FrameLedger.Agent.exe");
        string? error = new LogonTask().Install(exe);
        if (error is not null)
        {
            AgentConsole.Problem($"task: Task Scheduler refused: {error}");
            return Task.FromResult(_exitUsage);
        }

        AgentConsole.Line($"task: {LogonTask.Folder}\\{LogonTask.DefaultTaskName} runs \"{exe}\" {LogonTask.Arguments} at this user's logon, lowest privileges");
        return Task.FromResult(_exitOk);
    }

    private static Task<int> UninstallTaskAsync()
    {
        string? error = new LogonTask().Remove();
        if (error is not null)
        {
            AgentConsole.Problem($"task: Task Scheduler refused: {error}");
            return Task.FromResult(_exitUsage);
        }

        AgentConsole.Line($"task: {LogonTask.Folder}\\{LogonTask.DefaultTaskName} removed (or was not there)");
        return Task.FromResult(_exitOk);
    }
}
