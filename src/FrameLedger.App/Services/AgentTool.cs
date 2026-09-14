using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary><see cref="IAgentTool"/> over the Agent beside this executable: one flag, output captured, 30 s.</summary>
public sealed class AgentTool : IAgentTool
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    public static string AgentPath => Path.Combine(AppContext.BaseDirectory, "FrameLedger.Agent.exe");

    public async Task<AgentToolResult> RunAsync(string flag, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flag);
        if (!File.Exists(AgentPath))
        {
            return new AgentToolResult(-1, "FrameLedger.Agent.exe is not beside this executable");
        }

        try
        {
            using Process? process = Process.Start(new ProcessStartInfo(AgentPath, flag)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = AppContext.BaseDirectory,
            });
            if (process is null)
            {
                return new AgentToolResult(-1, "the Agent did not start");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(_timeout);
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            string output = (await stdout.ConfigureAwait(false) + await stderr.ConfigureAwait(false)).Trim();
            Log.Information("agent tool: {Flag} exit {Exit}: {Output}", flag, process.ExitCode, output);
            return new AgentToolResult(process.ExitCode, output);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or OperationCanceledException)
        {
            Log.Warning(ex, "agent tool: {Flag} did not run", flag);
            return new AgentToolResult(-1, ex.Message);
        }
    }
}
