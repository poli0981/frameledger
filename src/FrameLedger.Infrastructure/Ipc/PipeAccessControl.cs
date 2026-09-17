using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace FrameLedger.Infrastructure.Ipc;

/// <summary>
/// <c>07_IPC</c> §C's ACL as SDDL, and the client check that backs it: "current interactive user's SID +
/// Administrators; reject clients whose token user differs". The descriptor is what <c>CreateNamedPipe</c> is
/// handed; the check is what a connection has to pass before its first request is answered — and both are
/// stated here as code because <c>20_OPEN_QUESTIONS</c> §G asked for the implementable form, not the policy.
/// </summary>
public static class PipeAccessControl
{
    /// <summary>
    /// <c>O:&lt;user&gt;D:P(A;;GA;;;&lt;user&gt;)(A;;GA;;;BA)</c> — owned by the user, a protected DACL (no inheritance), full
    /// access to the user and to Administrators, nothing to anyone else. Not the ACL alone: an administrator of a DIFFERENT
    /// account passes it and is then refused by <see cref="ClientUserOf"/> against <see cref="CurrentUser"/>.
    /// </summary>
    /// <remarks>
    /// <b>The owner is stated, not defaulted</b> (2026-09-17). Left out, it is the creating token's default owner, and an
    /// elevated token's is <c>BUILTIN\Administrators</c> (the log files the elevated Agents of that morning created are owned
    /// by it; the unelevated ones' by the user) — so a pipe's owner would depend on how its Agent was started, and a client
    /// checking it would refuse the same user's Agent across elevation.
    /// </remarks>
    public static string Sddl(SecurityIdentifier user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return $"O:{user.Value}D:P(A;;GA;;;{user.Value})(A;;GA;;;BA)";
    }

    /// <summary>The self-relative binary form <c>SECURITY_ATTRIBUTES.lpSecurityDescriptor</c> points at.</summary>
    public static byte[] DescriptorBytes(string sddl)
    {
        var raw = new RawSecurityDescriptor(sddl);
        byte[] bytes = new byte[raw.BinaryLength];
        raw.GetBinaryForm(bytes, 0);
        return bytes;
    }

    /// <summary>This process's token user — the only user the pipe answers.</summary>
    public static SecurityIdentifier CurrentUser()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return identity.User ?? throw new InvalidOperationException("the current token carries no user SID");
    }

    /// <summary>
    /// The connected client's token user, read by impersonating it for the length of one call. Valid only after
    /// the client has written at least one byte (<c>ImpersonateNamedPipeClient</c> refuses before that), which
    /// is why the server checks it on the FIRST frame rather than on the connect.
    /// </summary>
    public static SecurityIdentifier? ClientUserOf(NamedPipeServerStream pipe)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        SecurityIdentifier? sid = null;
        pipe.RunAsClient(() =>
        {
            using WindowsIdentity client = WindowsIdentity.GetCurrent();
            sid = client.User;
        });
        return sid;
    }

    /// <summary>The owner of the pipe <paramref name="pipe"/> is connected to, read from its security descriptor.</summary>
    public static SecurityIdentifier? OwnerOf(PipeStream pipe)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        return pipe.GetAccessControl().GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
    }

    /// <summary>
    /// The client's half, run on a connected pipe before anything is written to it: the pipe must be owned by this process's
    /// token USER. <c>PipeOptions.CurrentUserOnly</c> compares it with the token's default OWNER instead, which an elevated
    /// token sets to Administrators — so an App started "as administrator" refused the same user's Agent, took the refusal
    /// for an absent Agent, and started another one on every connect round (2026-09-17, four Agents).
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">The pipe is owned by someone else.</exception>
    public static void RequireOwnedByCurrentUser(SecurityIdentifier? owner, SecurityIdentifier user, string pipeName)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (owner is null || !owner.Equals(user))
        {
            throw new UnauthorizedAccessException(
                $@"\\.\pipe\{pipeName} is owned by {owner?.Value ?? "nobody"}, not by this process's user {user.Value}");
        }
    }
}
