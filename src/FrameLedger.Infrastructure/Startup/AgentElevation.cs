using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace FrameLedger.Infrastructure.Startup;

/// <summary>
/// The Agent's admin mode (beta.10, owner decision D34): Windows is asked — a UAC prompt — EVERY time the Agent starts while
/// <c>capture.run_elevated</c> is on, by whoever starts it: the App (its own window owns the prompt) or an Agent the logon
/// task started (the prompt flashes on the taskbar). There is no silent path: no "highest privileges" task, no service.
/// </summary>
/// <remarks>
/// <para>
/// <b>The elevated Agent is told whose it is</b> (<c>--serve --for-user &lt;SID&gt;</c>) and checks it before it touches
/// anything: a standard user answers the prompt with an administrator's password, and the Agent that starts then runs as
/// THAT account — its <c>%LOCALAPPDATA%</c> is someone else's ledger. It exits <see cref="ExitOtherAccount"/>, and the
/// starter carries on with a standard user's Agent that says why.
/// </para>
/// <para>
/// <b>An Agent that was asked for and not granted says so</b> (<c>--serve --elevation &lt;outcome&gt;</c>, and
/// <c>HelloAck.ElevationOutcome</c>), and does not ask again: the answer was the user's.
/// </para>
/// </remarks>
public static class AgentElevation
{
    /// <summary>The exit code of an elevated Agent whose token belongs to another account than the one that asked.</summary>
    public const int ExitOtherAccount = 11;

    /// <summary><c>ERROR_CANCELLED</c>: the user answered the prompt No.</summary>
    public const int ErrorCancelled = 1223;

    /// <summary>The prompt was accepted by the account that asked; this Agent is elevated.</summary>
    public const string Granted = "granted";

    /// <summary>The prompt was answered No.</summary>
    public const string Declined = "declined";

    /// <summary>The prompt was accepted with another account's credentials, whose Agent refused to run.</summary>
    public const string OtherAccount = "other-account";

    /// <summary>Windows could not show the prompt or start the elevated Agent.</summary>
    public const string Failed = "failed";

    /// <summary>The outcomes a starter may pass on the command line (<c>--elevation</c>).</summary>
    public static readonly IReadOnlyList<string> Refusals = [Declined, OtherAccount, Failed];

    /// <summary>This process's token user, as the elevated Agent will compare it.</summary>
    public static string CurrentUserSid()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value ?? throw new InvalidOperationException("the current token carries no user SID");
    }

    /// <summary>Whether <paramref name="sid"/> is this process's token user.</summary>
    public static bool IsCurrentUser(string sid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sid);
        return string.Equals(CurrentUserSid(), sid, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Asks Windows to start <paramref name="agentPath"/> elevated with <paramref name="arguments"/>: the UAC prompt, owned by
    /// <paramref name="ownerWindow"/> when there is one. Blocks while the prompt is up. The process, or null with
    /// <see cref="Declined"/> or <see cref="Failed"/> in <paramref name="refusal"/>.
    /// </summary>
    public static Process? Start(string agentPath, string arguments, nint ownerWindow, out string? refusal, out int? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentPath);
        refusal = null;
        error = null;
        try
        {
            Process? started = Process.Start(new ProcessStartInfo(agentPath, arguments ?? string.Empty)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(agentPath) ?? string.Empty,
                // The window the prompt belongs to. ErrorDialog is what makes .NET pass it; a cancelled prompt shows no
                // error dialog, and a missing file is checked before this is ever called.
                ErrorDialog = ownerWindow != 0,
                ErrorDialogParentHandle = ownerWindow,
            });
            if (started is null)
            {
                refusal = Failed;
            }

            return started;
        }
        catch (Win32Exception ex)
        {
            error = ex.NativeErrorCode;
            refusal = ex.NativeErrorCode == ErrorCancelled ? Declined : Failed;
            return null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            refusal = Failed;
            return null;
        }
    }

    /// <summary>
    /// Waits until the elevated Agent has claimed <paramref name="dataDirectory"/> (true) or has exited (false, with its code),
    /// up to <paramref name="timeout"/> (false, no code: it neither claimed nor exited).
    /// </summary>
    public static async Task<(bool Claimed, int? ExitCode)> WaitForClaimAsync(Process agent, string dataDirectory, TimeSpan timeout,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (AgentInstanceLock.IsHeld(dataDirectory))
            {
                return (true, null);
            }

            if (agent.HasExited)
            {
                // The claim may have come and gone with a short-lived Agent; the exit code says which.
                return (false, SafeExitCode(agent));
            }

            await Task.Delay(100, ct).ConfigureAwait(false);
        }

        return (AgentInstanceLock.IsHeld(dataDirectory), null);
    }

    private static int? SafeExitCode(Process agent)
    {
        try
        {
            return agent.ExitCode;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return null;
        }
    }
}
