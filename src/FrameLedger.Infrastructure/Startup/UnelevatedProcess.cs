using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;

namespace FrameLedger.Infrastructure.Startup;

/// <summary>
/// Starts a program with the token the desktop shell runs with (beta.10, the Agent's admin mode, owner decision D34): what a
/// game started by an ELEVATED Agent gets — the same token it would get from a double-click — never the Agent's own.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it matters.</b> <c>LaunchGame</c> reaches the Agent over the pipe, which any process of the same user may open, and
/// a game row is a file path any such process could write into the ledger. An elevated Agent that started games with its own
/// token would be an elevation for everything running under the account, with no prompt. So an elevated Agent starts games
/// through this, and refuses to start one when it cannot (no shell to take the token from) rather than start it elevated.
/// </para>
/// <para>
/// <b>How.</b> The shell window's process (<c>GetShellWindow</c>), its token duplicated as a primary token, and
/// <c>CreateProcessWithTokenW</c> — documented, and it needs only <c>SeImpersonatePrivilege</c>, which an elevated
/// administrator's token holds enabled; nothing here enables a privilege. The explicit environment block is this process's
/// with the caller's variables on top, which is what <see cref="System.Diagnostics.ProcessStartInfo"/> would pass.
/// </para>
/// </remarks>
public static class UnelevatedProcess
{
    /// <summary><c>ERROR_INVALID_WINDOW_HANDLE</c>: there is no desktop shell window to take a standard user's token from.</summary>
    public const int ErrorNoShell = 1400;

    /// <summary>The desktop shell's process id (<c>GetShellWindow</c>'s owner), or null when there is no shell.</summary>
    public static unsafe int? ShellProcessId()
    {
        HWND shell = PInvoke.GetShellWindow();
        uint* pid = stackalloc uint[1];
        return shell.IsNull || PInvoke.GetWindowThreadProcessId(shell, pid) == 0 || *pid == 0 ? null : (int)*pid;
    }

    /// <summary>
    /// Starts <paramref name="exePath"/>; the process and its id, or null with the Win32 error in <paramref name="error"/>. The
    /// returned handle keeps the id from being reused until the caller has pinned the process its own way and disposed it.
    /// </summary>
    public static unsafe (int Pid, IDisposable Process)? Start(string exePath, string arguments, string workingDirectory,
        IReadOnlyDictionary<string, string> environment, out int error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        ArgumentNullException.ThrowIfNull(environment);
        error = 0;

        if (ShellProcessId() is not { } shellPid)
        {
            error = ErrorNoShell;
            return null;
        }

        HANDLE shellProcess = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)shellPid);
        if (shellProcess.IsNull)
        {
            error = Marshal.GetLastSystemError();
            return null;
        }

        HANDLE shellToken = default;
        HANDLE primary = default;
        try
        {
            if (!PInvoke.OpenProcessToken(shellProcess, TOKEN_ACCESS_MASK.TOKEN_DUPLICATE, &shellToken))
            {
                error = Marshal.GetLastSystemError();
                return null;
            }

            const TOKEN_ACCESS_MASK access = TOKEN_ACCESS_MASK.TOKEN_QUERY | TOKEN_ACCESS_MASK.TOKEN_DUPLICATE | TOKEN_ACCESS_MASK.TOKEN_ASSIGN_PRIMARY
                | TOKEN_ACCESS_MASK.TOKEN_ADJUST_DEFAULT | TOKEN_ACCESS_MASK.TOKEN_ADJUST_SESSIONID;
            if (!PInvoke.DuplicateTokenEx(shellToken, access, null, SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenPrimary, &primary))
            {
                error = Marshal.GetLastSystemError();
                return null;
            }

            return Create(primary, exePath, arguments, workingDirectory, environment, out error);
        }
        finally
        {
            if (!primary.IsNull)
            {
                _ = PInvoke.CloseHandle(primary);
            }

            if (!shellToken.IsNull)
            {
                _ = PInvoke.CloseHandle(shellToken);
            }

            _ = PInvoke.CloseHandle(shellProcess);
        }
    }

    /// <summary>The block <c>CreateProcessWithTokenW</c> takes: <c>name=value\0</c> per variable, sorted, then <c>\0</c>.</summary>
    public static string EnvironmentBlock(IReadOnlyDictionary<string, string> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        var merged = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string name && name.Length > 0 && entry.Value is string value)
            {
                merged[name] = value;
            }
        }

        foreach ((string name, string value) in overrides)
        {
            merged[name] = value;
        }

        var block = new StringBuilder();
        foreach ((string name, string value) in merged)
        {
            block.Append(name).Append('=').Append(value).Append('\0');
        }

        return block.Append('\0').ToString();
    }

    private static unsafe (int Pid, IDisposable Process)? Create(HANDLE token, string exePath, string arguments, string workingDirectory,
        IReadOnlyDictionary<string, string> environment, out int error)
    {
        error = 0;
        // The command line is written to by the call, so it is a buffer of its own; the program is named in quotes, as
        // Process.Start would name it.
        char[] commandLine = ("\"" + exePath + "\"" + (string.IsNullOrEmpty(arguments) ? string.Empty : " " + arguments) + "\0").ToCharArray();
        string block = EnvironmentBlock(environment);
        var startup = new STARTUPINFOW { cb = (uint)sizeof(STARTUPINFOW) };
        PROCESS_INFORMATION started;
        fixed (char* application = exePath)
        fixed (char* line = commandLine)
        fixed (char* env = block)
        fixed (char* directory = workingDirectory)
        {
            if (!PInvoke.CreateProcessWithToken(token, default, new PCWSTR(application), new PWSTR(line),
                    PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT, env, string.IsNullOrEmpty(workingDirectory) ? default : new PCWSTR(directory),
                    &startup, &started))
            {
                error = Marshal.GetLastSystemError();
                return null;
            }
        }

        _ = PInvoke.CloseHandle(started.hThread);
        return ((int)started.dwProcessId, new ProcessHandle(started.hProcess));
    }

    private sealed class ProcessHandle(HANDLE handle) : IDisposable
    {
        private HANDLE _handle = handle;

        public void Dispose()
        {
            if (!_handle.IsNull)
            {
                _ = PInvoke.CloseHandle(_handle);
                _handle = default;
            }
        }
    }
}
