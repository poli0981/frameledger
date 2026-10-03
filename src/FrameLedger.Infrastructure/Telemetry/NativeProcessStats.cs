// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;
using FrameLedger.Infrastructure.Native;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// <c>FrameLedger.ProcessStats.dll</c> behind <see cref="IProcessStats"/> (beta.12, owner decision D43): the third P/Invoke
/// facade after <c>NativeAntiCheatGuard</c> and <c>NativeNvapiBridge</c>, loaded the same way — by absolute path from beside
/// this assembly, never from a search path.
/// </summary>
/// <remarks>
/// <para>
/// <b>Absent is not a fault.</b> A build without the native tree has no DLL beside the binary: <see cref="Open"/> answers
/// <see cref="IntPtr.Zero"/> and every game-memory field of every session reads N/A, the way a missing telemetry layer
/// does. The guard's absence is a safety hole and throws; this one's is a missing measurement and reports itself.
/// </para>
/// <para>
/// <b>Never loaded into a game.</b> The Agent loads it into its own process. What it reads about a game — the
/// <c>GPU Process Memory</c> counters and <c>GetProcessMemoryInfo</c> — is the kernel's accounting about the process,
/// never the process's memory (CLAUDE.md rule 4).
/// </para>
/// </remarks>
public sealed class NativeProcessStats : IProcessStats
{
    /// <summary><c>FL_PS_ABI_VERSION</c>, what this mirror was written against.</summary>
    public const uint AbiVersion = 1;

    /// <summary>What <see cref="Read"/> answers when the DLL is not beside this assembly at all.</summary>
    public const int Unavailable = -1999;

    private const string _dll = "FrameLedger.ProcessStats.dll";

    static NativeProcessStats()
    {
        // Optional, like the NVAPI bridge: absent is a zero handle, a DllNotFoundException under the System32-only
        // default search, and N/A in every game-memory field -- never a search of anything else.
        BesideThisAssembly.Claim(_dll, required: false);
    }

    /// <summary>Whether the DLL is beside this assembly, without loading it.</summary>
    public static bool IsPresent => File.Exists(BesideThisAssembly.PathOf(_dll));

    /// <summary>The DLL's ABI version, or null when it is absent.</summary>
    public static uint? LoadedAbiVersion => Guard(static () => (uint?)FlPsAbiVersion(), null);

    /// <summary>The DLL's <c>sizeof(FlPsSample)</c>, or null when it is absent.</summary>
    public static uint? LoadedSampleSize => Guard(static () => (uint?)FlPsSampleSize(), null);

    /// <summary>The DLL's build id, or null when it is absent.</summary>
    public static string? LoadedBuildId => Guard(static () => Marshal.PtrToStringAnsi(FlPsBuildId()), null);

    public IntPtr Open() => IsPresent ? Guard(FlPsOpen, IntPtr.Zero) : IntPtr.Zero;

    public int Collect(IntPtr reader) => reader == IntPtr.Zero ? Unavailable : Guard(() => FlPsCollect(reader), Unavailable);

    public int Read(IntPtr reader, uint pid, ulong creationTime, SafeHandle? process, ref ProcessStatsSample sample)
    {
        if (reader == IntPtr.Zero)
        {
            return Unavailable;
        }

        sample.Size = (uint)Marshal.SizeOf<ProcessStatsSample>();
        try
        {
            // A held handle goes through the marshaller, which add-refs it for the call: a pin released mid-read throws
            // ObjectDisposedException here rather than handing the DLL a closed handle.
            return process is null
                ? FlPsRead(reader, pid, creationTime, IntPtr.Zero, ref sample)
                : FlPsReadHeld(reader, pid, creationTime, process, ref sample);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return Unavailable;
        }
    }

    public void Close(IntPtr reader)
    {
        if (reader != IntPtr.Zero)
        {
            Guard(() =>
            {
                FlPsClose(reader);
                return 0;
            }, 0);
        }
    }

    private static T Guard<T>(Func<T> call, T absent)
    {
        try
        {
            return call();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return absent;
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr FlPsOpen();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void FlPsClose(IntPtr reader);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int FlPsCollect(IntPtr reader);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int FlPsRead(IntPtr reader, uint pid, ulong creationTime, IntPtr process, ref ProcessStatsSample sample);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_dll, EntryPoint = "FlPsRead", CallingConvention = CallingConvention.Cdecl)]
    private static extern int FlPsReadHeld(IntPtr reader, uint pid, ulong creationTime, SafeHandle process, ref ProcessStatsSample sample);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern uint FlPsAbiVersion();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern uint FlPsSampleSize();

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport(_dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr FlPsBuildId();
}
