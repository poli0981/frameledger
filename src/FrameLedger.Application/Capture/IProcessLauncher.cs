namespace FrameLedger.Application.Capture;

/// <summary>
/// Launch mode's first step (<c>04_CAPTURE</c> §Launch mode): start the consented executable and
/// hold it from birth. Never <c>CREATE_SUSPENDED</c>; never terminates what it started.
/// </summary>
public interface IProcessLauncher
{
    /// <summary>The new pid and its held liveness — the caller owns the liveness — or null when it could not be started or pinned.</summary>
    (int Pid, ITargetLiveness Alive)? Start(string exePath, string arguments);

    /// <summary>
    /// The same, saying why a start failed: the Win32 error (2 file not found, 5 access denied, 740 elevation required…).
    /// A launcher that cannot tell answers null, which is what this default does (beta.8).
    /// </summary>
    (int Pid, ITargetLiveness Alive)? Start(string exePath, string arguments, out int? win32Error)
    {
        win32Error = null;
        return Start(exePath, arguments);
    }
}
