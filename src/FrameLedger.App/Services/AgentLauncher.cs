using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary>Starts the Agent beside this executable, as its own process — it must outlive the App (background capture).</summary>
public sealed class AgentLauncher : IAgentLauncher
{
    private static string AgentPath => Path.Combine(AppContext.BaseDirectory, "FrameLedger.Agent.exe");

    public bool CanLaunch => File.Exists(AgentPath);

    public bool TryStart()
    {
        if (!CanLaunch)
        {
            return false;
        }

        try
        {
            using Process? started = Process.Start(new ProcessStartInfo(AgentPath, "--serve")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory,
            });
            Log.Information("agent: started {Path} --serve (pid {Pid})", AgentPath, started?.Id);
            return started is not null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            Log.Warning(ex, "agent: could not start {Path}", AgentPath);
            return false;
        }
    }
}
