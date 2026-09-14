using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using FrameLedger.App.Services;
using FrameLedger.Infrastructure.Ipc;
using FrameLedger.Infrastructure.Startup;
using FrameLedger.Infrastructure.Vulkan;
using FrameLedger.Shared.Ipc;
using Velopack;

namespace FrameLedger.App.Update;

/// <summary>
/// What runs inside Velopack's own process launches, before <see cref="App"/> exists (<c>Program.Main</c>, first
/// line). Install registers nothing machine-wide — the first-run flow offers the Agent's task and the layer's
/// registration follows the ledger (<c>12_BUILD</c> §The Vulkan layer is not registered at install time). Uninstall
/// is <see cref="UninstallHook"/> over the real pieces. A restart after an update sets <see cref="UpdateRestart"/>
/// so the host re-validates the logon task's action path.
/// </summary>
internal static class VelopackHooks
{
    /// <summary>
    /// Velopack gives the callback 30 s in all, and the question at the end needs most of them: 1 s to find a pipe
    /// (none is the common case), 3 s for the ack, 5 s for the Agent's process to leave.
    /// </summary>
    private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan _exitTimeout = TimeSpan.FromSeconds(5);

    public static VelopackApp Configure(VelopackApp app) => app
        .OnRestarted(static _ => UpdateRestart.Detected = true)
        .OnBeforeUninstallFastCallback(static _ => BeforeUninstall());

    private static void BeforeUninstall()
    {
        string manifest = Path.Combine(VkLayerLaunchEnvironment.DefaultDirectory, VkLayerLaunchEnvironment.ManifestFileName);
        var hook = new UninstallHook(
            stopAgent: StopAgent,
            unregisterLayer: () => new VkLayerRegistration().Unregister(manifest),
            removeTask: static () => new LogonTask().Remove() is null,
            askDeleteData: static () => UninstallHook.AreSeparate(UiPaths.DataDirectory, AppContext.BaseDirectory)
                ? System.Windows.MessageBox.Show(Strings.Uninstall_DataFolder_Body, Strings.Uninstall_DataFolder_Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes
                : throw new InvalidOperationException($"the install ({AppContext.BaseDirectory}) and the data folder ({UiPaths.DataDirectory}) overlap; nothing is deleted"),
            deleteData: static () => Directory.Delete(UiPaths.DataDirectory, recursive: true),
            log: Log);
        hook.Run();
    }

    /// <summary>
    /// The Agent outlives the App and holds the install directory and the ledger; a <c>Shutdown</c> over the pipe,
    /// best effort, then a bounded wait for its process to leave — so the uninstaller's file removal and a Yes to the
    /// data question do not meet an open <c>ledger.db</c>. Nothing is killed.
    /// </summary>
    [SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Velopack's fast callback is synchronous, runs with no dispatcher, and the process exits when it returns; a bounded wait on a pooled task is the only shape available")]
    private static bool StopAgent()
    {
        Task<bool> ask = Task.Run(static async () =>
        {
            var client = new PipeClient();
            await using (client.ConfigureAwait(false))
            {
                await client.ConnectAsync(_connectTimeout).ConfigureAwait(false);
                await client.RequestEnvelopeAsync(IpcMessageType.Shutdown, new ShutdownRequest(), _requestTimeout).ConfigureAwait(false);
                return true;
            }
        });
        bool asked;
        try
        {
            asked = ask.Wait(_connectTimeout + _requestTimeout + _connectTimeout) && ask.Result;
        }
        catch (AggregateException)
        {
            // No Agent listening (the common case: nothing was running), or it dropped the pipe on Shutdown.
            asked = false;
        }

        WaitForAgentExit();
        return asked;
    }

    private static void WaitForAgentExit()
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + _exitTimeout;
        foreach (Process agent in Process.GetProcessesByName("FrameLedger.Agent"))
        {
            using (agent)
            {
                TimeSpan left = deadline - DateTimeOffset.UtcNow;
                if (left > TimeSpan.Zero)
                {
                    try
                    {
                        _ = agent.WaitForExit(left);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                    {
                        // Gone between the listing and the wait, or not ours to wait on.
                    }
                }
            }
        }
    }

    /// <summary>Serilog is not configured this early; the hook's own file under <c>logs/</c>, appended, never thrown from.</summary>
    private static void Log(string line)
    {
        try
        {
            if (!Directory.Exists(UiPaths.DataDirectory))
            {
                // Deleted on the user's yes a moment ago; recreating it for a log line would undo their answer.
                return;
            }

            Directory.CreateDirectory(UiPaths.Logs);
            File.AppendAllText(Path.Combine(UiPaths.Logs, "uninstall.log"), $"[{DateTimeOffset.UtcNow:O}] {line}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The folder may already be gone (the user said yes) — nothing to write to, nothing to do.
        }
    }
}
