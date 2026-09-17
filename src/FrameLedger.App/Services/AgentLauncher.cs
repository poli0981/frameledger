using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using FrameLedger.Infrastructure.Startup;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary>Starts the Agent beside this executable, as its own process — it must outlive the App (background capture).</summary>
/// <remarks>
/// Never a second one (2026-09-17): <see cref="RunningAgent"/> probes the data folder's <see cref="AgentInstanceLock"/>,
/// and the last Agent this App started is watched until it exits, with its exit code logged — exit
/// <see cref="AgentInstanceLock.ExitHeldElsewhere"/> is an Agent that found the folder already claimed.
/// </remarks>
public sealed class AgentLauncher : IAgentLauncher
{
    private readonly string _dataDirectory;
    private Task _lastStarted = Task.CompletedTask;

    public AgentLauncher()
        : this(UiPaths.DataDirectory)
    {
    }

    /// <summary>A launcher that probes <paramref name="dataDirectory"/>'s claim — a test's folder, never the user's.</summary>
    internal AgentLauncher(string dataDirectory) =>
        _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));

    private static string AgentPath => Path.Combine(AppContext.BaseDirectory, "FrameLedger.Agent.exe");

    public bool CanLaunch => File.Exists(AgentPath);

    /// <inheritdoc />
    public string? RunningAgent =>
        AgentInstanceLock.IsHeld(_dataDirectory) ? $"an Agent already holds {_dataDirectory}"
        : !_lastStarted.IsCompleted ? "the Agent this App started last is still starting"
        : null;

    public bool TryStart()
    {
        if (!CanLaunch)
        {
            return false;
        }

        Process? started;
        try
        {
            started = Process.Start(new ProcessStartInfo(AgentPath, "--serve")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory,
            });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            Log.Warning(ex, "agent: could not start {Path}", AgentPath);
            return false;
        }

        if (started is null)
        {
            return false;
        }

        // An elevated App starts an elevated Agent: the token is inherited, and it is what 07_IPC §C's owner check is about.
        Log.Information("agent: started {Path} --serve (pid {Pid}{Elevated})", AgentPath, started.Id,
            Environment.IsPrivilegedProcess ? ", elevated like this App" : string.Empty);
        _lastStarted = WatchUntilExitAsync(started);
        return true;
    }

    private static async Task WatchUntilExitAsync(Process agent)
    {
        using (agent)
        {
            int pid = agent.Id;
            await agent.WaitForExitAsync().ConfigureAwait(false);
            int code = agent.ExitCode;
            Log.Information("agent: pid {Pid} exited {Code}{Meaning}", pid, code,
                code == AgentInstanceLock.ExitHeldElsewhere ? " — another Agent already holds the data folder" : string.Empty);
        }
    }
}
