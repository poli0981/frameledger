// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FrameLedger.Infrastructure.Io;

/// <summary>
/// A process's image path as the kernel names it — <c>QueryFullProcessImageNameW</c> — normalised the way
/// <see cref="ExecutableIdentity"/> normalises a consent record's. The ONE spelling the capture path compares (beta.33,
/// D80): the watcher keys a game on it, and <c>TargetResolver</c> re-finds the game by it just before injecting.
/// </summary>
/// <remarks>
/// <para>
/// The resolver read the LOADER's name until beta.33 (<c>Process.MainModule</c>): the spelling a process was started
/// with, where the watcher had the kernel's. For a game started through a junction — a package Windows activates from
/// <c>C:\Program Files\WindowsApps\&lt;package&gt;</c>, which is a junction to the game's own folder — the two could
/// differ, and the session would end <c>TargetNotRunning</c> about a game the watcher had just seen. <c>spike-notes</c>
/// §25 measured them equal on the owner's eleven Xbox titles; one source keeps them equal by construction.
/// </para>
/// <para>
/// Nothing here reads the process's memory: the kernel copies the name into a buffer we own. Null is "could not
/// read", never a name — a caller must not let it narrow or widen a set.
/// </para>
/// </remarks>
public static class ProcessImagePath
{
    /// <summary><c>PROCESS_QUERY_LIMITED_INFORMATION</c>: the least right that answers, which the watcher holds on most processes.</summary>
    public const uint QueryLimited = 0x1000;

    /// <summary>
    /// <c>PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ</c>: the rights the guard's module check opens a target with
    /// (<c>fl_guard_sources.cpp</c>). A process that refuses them — one an anti-cheat driver closes, or an elevated game
    /// to an unelevated Agent — cannot pass the guard, and the resolver says so before asking it (D21).
    /// </summary>
    public const uint GuardRights = QueryLimited | 0x0010;

    private const int _maxPath = 32_767;

    /// <summary>The normalised image path of <paramref name="pid"/> opened with <paramref name="access"/>, or null with the Win32 error.</summary>
    /// <param name="pid">The process.</param>
    /// <param name="access"><see cref="QueryLimited"/> or <see cref="GuardRights"/>.</param>
    /// <param name="error">The error that stopped the read; 0 when the name was read and would not normalise.</param>
    public static string? Read(int pid, uint access, out int error)
    {
        error = 0;
        if (pid <= 0)
        {
            return null;
        }

        using SafeProcessHandle handle = OpenProcess(access, false, unchecked((uint)pid));
        if (handle.IsInvalid)
        {
            error = Marshal.GetLastPInvokeError();
            return null;
        }

        return Of(handle, out error);
    }

    /// <summary>The normalised image path of the process <paramref name="handle"/> names, or null with the Win32 error.</summary>
    public static string? Of(SafeProcessHandle handle, out int error)
    {
        ArgumentNullException.ThrowIfNull(handle);
        error = 0;
        char[] buffer = new char[_maxPath];
        uint size = (uint)buffer.Length;
        if (!QueryFullProcessImageNameW(handle, 0, buffer, ref size) || size == 0)
        {
            error = Marshal.GetLastPInvokeError();
            return null;
        }

        try
        {
            return ExecutableIdentity.Normalise(new string(buffer, 0, checked((int)size)));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(SafeProcessHandle process, uint flags,
        [Out] char[] exeName, ref uint size);
}
