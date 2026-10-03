// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using FrameLedger.Infrastructure.Ipc;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;

namespace FrameLedger.Infrastructure.Startup;

/// <summary>
/// A named mutex whose security descriptor is stated rather than defaulted (beta.10, the Agent's admin mode, D34): owned by
/// this process's user, full access to the user, Administrators and SYSTEM, nothing to anyone else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not <see cref="Mutex"/>.</b> A mutex takes the creating token's default DACL, and an elevated token's grants
/// Administrators and SYSTEM and not the user — the trap <see cref="PipeAccessControl"/> and the tolerance channel already
/// state. The ledger's migration lock is taken by every process that opens the ledger: an elevated Agent that created it
/// while migrating would have refused the same user's unelevated App with an access-denied at the one moment both open the
/// ledger — at logon, after an update.
/// </para>
/// <para>
/// <b>Held, then released.</b> Unlike the Agent's instance lock (created, never waited on), this one is waited on: two
/// processes serialise on it. An abandoned wait — the holder died mid-migration — is the lock taken, and says so.
/// </para>
/// </remarks>
public sealed class SharedMutex : IDisposable
{
    private HANDLE _handle;

    private SharedMutex(HANDLE handle) => _handle = handle;

    /// <summary><c>O:&lt;user&gt;D:P(A;;GA;;;&lt;user&gt;)(A;;GA;;;BA)(A;;GA;;;SY)</c>: the user's, whoever created it.</summary>
    public static string Sddl(SecurityIdentifier user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return $"O:{user.Value}D:P(A;;GA;;;{user.Value})(A;;GA;;;BA)(A;;GA;;;SY)";
    }

    /// <summary>Creates <paramref name="name"/>, or opens it when another process of this user created it first.</summary>
    /// <exception cref="Win32Exception">It could be neither created nor opened.</exception>
    public static unsafe SharedMutex CreateOrOpen(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        byte[] descriptor = PipeAccessControl.DescriptorBytes(Sddl(PipeAccessControl.CurrentUser()));
        fixed (char* n = name)
        fixed (byte* sd = descriptor)
        {
            var attributes = new SECURITY_ATTRIBUTES
            {
                nLength = (uint)sizeof(SECURITY_ATTRIBUTES),
                lpSecurityDescriptor = sd,
                bInheritHandle = false,
            };
            HANDLE handle = PInvoke.CreateMutex(&attributes, false, new PCWSTR(n));
            // Read at once: the generated declaration may not set the last P/Invoke error (PipeServer records the same).
            int error = Marshal.GetLastSystemError();
            if (handle.IsNull)
            {
                throw new Win32Exception(error, $"CreateMutex({name}): {Marshal.GetPInvokeErrorMessage(error)} (error {error})");
            }

            return new SharedMutex(handle);
        }
    }

    /// <summary>Waits up to <paramref name="timeout"/>; true when the lock is this process's (an abandoned one included).</summary>
    public bool Wait(TimeSpan timeout, out bool abandoned)
    {
        ObjectDisposedException.ThrowIf(_handle.IsNull, this);
        uint ms = (uint)Math.Clamp(timeout.TotalMilliseconds, 0, uint.MaxValue - 1);
        WAIT_EVENT result = PInvoke.WaitForSingleObject(_handle, ms);
        abandoned = result == WAIT_EVENT.WAIT_ABANDONED;
        return result is WAIT_EVENT.WAIT_OBJECT_0 or WAIT_EVENT.WAIT_ABANDONED;
    }

    /// <summary>Releases a lock this thread holds.</summary>
    public void Release()
    {
        ObjectDisposedException.ThrowIf(_handle.IsNull, this);
        _ = PInvoke.ReleaseMutex(_handle);
    }

    public void Dispose()
    {
        if (!_handle.IsNull)
        {
            _ = PInvoke.CloseHandle(_handle);
            _handle = default;
        }
    }
}
