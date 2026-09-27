using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using FrameLedger.Infrastructure.Persistence;
using FrameLedger.Infrastructure.Startup;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary>Starts the Agent beside this executable, as its own process — it must outlive the App (background capture).</summary>
/// <remarks>
/// <para>
/// Never a second one (2026-09-17): <see cref="RunningAgent"/> probes the data folder's <see cref="AgentInstanceLock"/>,
/// and the last Agent this App started is watched until it exits, with its exit code logged — exit
/// <see cref="AgentInstanceLock.ExitHeldElsewhere"/> is an Agent that found the folder already claimed.
/// </para>
/// <para>
/// <b>The admin mode</b> (beta.10, owner decision D34). With <c>capture.run_elevated</c> on and this App a standard user's,
/// Windows is asked — a UAC prompt owned by the App's window — every time this App starts the Agent. Accepted: the Agent
/// starts as administrator for THIS user (<c>--for-user</c>). Declined: the Agent starts as a standard user and says so
/// (<c>--elevation declined</c>), and the answer is not asked again for that start. Answered with another account: that
/// account's Agent refuses to run (exit 11), and until this App restarts it starts the standard user's instead of asking
/// again. With the option off and this App elevated, the Agent starts with the desktop shell's token — the option is the
/// only thing that makes the Agent an administrator's. While a prompt is up the folder's <see cref="ElevationMarker"/> is
/// held, so the logon task's Agent does not prompt beside it.
/// </para>
/// </remarks>
public sealed class AgentLauncher : IAgentLauncher
{
    private readonly string _dataDirectory;
    private readonly Func<bool> _runElevated;
    private readonly Func<nint> _ownerWindow;
    private Task _lastStarted = Task.CompletedTask;
    private volatile bool _otherAccountAnswered;

    public AgentLauncher()
        : this(UiPaths.DataDirectory)
    {
    }

    /// <summary>A launcher that probes <paramref name="dataDirectory"/>'s claim — a test's folder, never the user's.</summary>
    internal AgentLauncher(string dataDirectory, Func<bool>? runElevated = null, Func<nint>? ownerWindow = null)
    {
        _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        _runElevated = runElevated ?? (() => RunElevatedSetting.Read(Path.Combine(_dataDirectory, LedgerPaths.DatabaseFileName)));
        _ownerWindow = ownerWindow ?? (static () => 0);
    }

    /// <summary>The launcher the App composes: the user's folder, and the main window as the prompt's owner.</summary>
    public static AgentLauncher ForApp(Func<nint> ownerWindow) => new(UiPaths.DataDirectory, ownerWindow: ownerWindow);

    private static string AgentPath => Path.Combine(AppContext.BaseDirectory, "FrameLedger.Agent.exe");

    public bool CanLaunch => File.Exists(AgentPath);

    /// <inheritdoc />
    public string? RunningAgent =>
        AgentInstanceLock.IsHeld(_dataDirectory) ? $"an Agent already holds {_dataDirectory}"
        : ElevationMarker.IsHeld(_dataDirectory) ? "an Agent is being started as administrator (a UAC prompt may be waiting on the taskbar)"
        : !_lastStarted.IsCompleted ? "the Agent this App started last is still starting"
        : null;

    /// <inheritdoc />
    public bool WillAskForElevation => !Environment.IsPrivilegedProcess && !_otherAccountAnswered && _runElevated();

    public bool TryStart()
    {
        if (!CanLaunch)
        {
            return false;
        }

        bool wanted = _runElevated();
        if (wanted && !Environment.IsPrivilegedProcess)
        {
            return _otherAccountAnswered ? StartPlain("--serve --elevation " + AgentElevation.OtherAccount) : StartElevated();
        }

        return !wanted && Environment.IsPrivilegedProcess ? StartWithShellToken() : StartPlain("--serve");
    }

    /// <summary>The UAC prompt; declined or failed, the standard user's Agent, which says so.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2025:Ensure tasks using 'IDisposable' instances complete before the instances are disposed",
        Justification = "the process and the marker are handed to the watch task, which disposes both; this method disposes neither after the handover")]
    private bool StartElevated()
    {
        ElevationMarker? marker = ElevationMarker.TryAcquire(_dataDirectory);
        if (marker is null)
        {
            // Someone else's prompt is up (the logon task's Agent): its answer starts the Agent; this round starts nothing.
            Log.Information("agent: another start is asking for administrator rights; not asking twice");
            return false;
        }

        Log.Information("agent: asking Windows to start the Agent as administrator (capture.run_elevated)");
        Process? started = AgentElevation.Start(AgentPath, "--serve --for-user " + AgentElevation.CurrentUserSid(), _ownerWindow(), out string? refusal, out int? error);
        if (started is null)
        {
            marker.Dispose();
            Log.Information("agent: the administrator prompt was {Refusal} (error {Error}); starting the Agent as a standard user", refusal, error);
            return StartPlain("--serve --elevation " + (refusal ?? AgentElevation.Failed));
        }

        Log.Information("agent: started {Path} as administrator (pid {Pid})", AgentPath, started.Id);
        _lastStarted = WatchElevatedAsync(started, marker);
        return true;
    }

    /// <summary>This App is elevated and the option is off: the Agent gets the desktop shell's token, as a double-click would.</summary>
    private bool StartWithShellToken()
    {
        (int Pid, IDisposable Process)? started = UnelevatedProcess.Start(AgentPath, "--serve", AppContext.BaseDirectory,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), out int error);
        if (started is not { } s)
        {
            // No shell to take a standard user's token from: the Agent inherits this App's elevation, as before beta.10, and
            // the Settings page says it runs elevated.
            Log.Warning("agent: could not start the Agent as a standard user (error {Error}); it inherits this App's elevation", error);
            return StartPlain("--serve");
        }

        Process? agent = Watchable(s);
        if (agent is not null)
        {
            Log.Information("agent: started {Path} --serve (pid {Pid}) as a standard user; this App is elevated and the admin mode is off", AgentPath, s.Pid);
            _lastStarted = WatchUntilExitAsync(agent);
        }

        return true;
    }

    /// <summary>The started Agent as a <see cref="Process"/>, taken while the start's own handle still holds its id; null when it is gone already.</summary>
    private static Process? Watchable((int Pid, IDisposable Process) started)
    {
        using (started.Process)
        {
            try
            {
                return Process.GetProcessById(started.Pid);
            }
            catch (ArgumentException)
            {
                // Gone already; its exit code is lost with it, and the next round says whether an Agent answers.
                return null;
            }
        }
    }

    private bool StartPlain(string arguments)
    {
        Process? started;
        try
        {
            started = Process.Start(new ProcessStartInfo(AgentPath, arguments)
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

        // An elevated App starts an elevated Agent here only when the admin mode is on (the token is inherited) or when no
        // standard user's token could be had; 07_IPC §C's owner check is what makes either reachable.
        Log.Information("agent: started {Path} {Arguments} (pid {Pid}{Elevated})", AgentPath, arguments, started.Id,
            Environment.IsPrivilegedProcess ? ", elevated like this App" : string.Empty);
        _lastStarted = WatchUntilExitAsync(started);
        return true;
    }

    /// <summary>Holds the marker until the elevated Agent claims the folder or exits; exit 11 is remembered for this App's life.</summary>
    private async Task WatchElevatedAsync(Process agent, ElevationMarker marker)
    {
        using (marker)
        {
            await AgentElevation.WaitForClaimAsync(agent, _dataDirectory, TimeSpan.FromMinutes(2)).ConfigureAwait(false);
        }

        int? code = await WaitForExitCodeAsync(agent).ConfigureAwait(false);
        if (code == AgentElevation.ExitOtherAccount)
        {
            _otherAccountAnswered = true;
            Log.Warning("agent: the administrator prompt was answered with another account, whose Agent refused to run; "
                + "until this App restarts the Agent starts as a standard user");
        }
    }

    private static async Task WatchUntilExitAsync(Process agent) => await WaitForExitCodeAsync(agent).ConfigureAwait(false);

    private static async Task<int?> WaitForExitCodeAsync(Process agent)
    {
        using (agent)
        {
            int pid = agent.Id;
            await agent.WaitForExitAsync().ConfigureAwait(false);
            int? code;
            try
            {
                code = agent.ExitCode;
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                code = null;
            }

            Log.Information("agent: pid {Pid} exited {Code}{Meaning}", pid, code,
                code == AgentInstanceLock.ExitHeldElsewhere ? " — another Agent already holds the data folder"
                : code == AgentElevation.ExitOtherAccount ? " — it ran as another account than this App's user"
                : string.Empty);
            return code;
        }
    }
}
