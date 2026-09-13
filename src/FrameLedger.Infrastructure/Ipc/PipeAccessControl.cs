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
    /// <c>D:P(A;;GA;;;&lt;user&gt;)(A;;GA;;;BA)</c> — a protected DACL (no inheritance), full access to the user
    /// and to Administrators, nothing to anyone else. Not the ACL alone: an administrator of a DIFFERENT account
    /// passes it and is then refused by <see cref="ClientUserOf"/> against <see cref="CurrentUser"/>.
    /// </summary>
    public static string Sddl(SecurityIdentifier user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return $"D:P(A;;GA;;;{user.Value})(A;;GA;;;BA)";
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
}
