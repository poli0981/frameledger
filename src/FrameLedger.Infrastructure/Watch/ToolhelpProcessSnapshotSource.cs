using System.Runtime.InteropServices;
using FrameLedger.Application.Watch;
using FrameLedger.Infrastructure.Io;
using Microsoft.Win32.SafeHandles;

namespace FrameLedger.Infrastructure.Watch;

/// <summary>
/// <see cref="IProcessSnapshotSource"/> over <c>CreateToolhelp32Snapshot</c> (P2 PR-F, <c>04_CAPTURE</c>
/// §Process watcher): pid, parent pid and image name from the snapshot itself; the full image path and
/// the creation time through a <c>PROCESS_QUERY_LIMITED_INFORMATION</c> handle, which is the least right
/// that answers and the one an unelevated Agent has on most processes.
/// </summary>
/// <remarks>
/// <para>
/// A process that refuses even that right — another user's, a protected one, one that exited between the
/// snapshot and the open — is listed with <c>ImagePath</c> and <c>StartedAt</c> null: it exists, and the
/// watcher must not pretend otherwise, but it can match nothing (<c>TargetResolver</c>'s "could not look
/// must not widen the set").
/// </para>
/// <para>
/// The image path is normalised the way <c>ExecutableIdentity</c> normalises a consent record's, so the
/// watcher's comparison is path against path and never depends on how a launcher spelled a junction.
/// </para>
/// <para>
/// Hand-written <c>DllImport</c>s beside <c>HeldProcessHandle</c>'s rather than CsWin32: the Toolhelp entry
/// is a fixed-size struct walk, and the marshaling-off CsWin32 profile this assembly uses for DXGI would
/// hand back raw <c>PWSTR</c>s for exactly the two strings this class exists to read.
/// </para>
/// </remarks>
public sealed class ToolhelpProcessSnapshotSource : IProcessSnapshotSource
{
    private const uint _snapProcess = 0x00000002;
    private const uint _queryLimitedInformation = 0x1000;
    private const int _maxPath = 32_767;
    private static readonly IntPtr _invalidHandle = new(-1);

    /// <inheritdoc />
    public IReadOnlyList<ProcessSnapshot> Take()
    {
        IntPtr snapshot = CreateToolhelp32Snapshot(_snapProcess, 0);
        if (snapshot == _invalidHandle || snapshot == IntPtr.Zero)
        {
            return [];
        }

        try
        {
            List<ProcessSnapshot> found = [];
            var entry = new ProcessEntry32W { dwSize = (uint)Marshal.SizeOf<ProcessEntry32W>() };
            if (!Process32FirstW(snapshot, ref entry))
            {
                return found;
            }

            do
            {
                int pid = unchecked((int)entry.th32ProcessID);
                if (pid <= 4)
                {
                    // Idle (0) and System (4): not ours to open, never a game.
                    continue;
                }

                (string? path, DateTimeOffset? started) = Describe(pid);
                found.Add(new ProcessSnapshot(pid, unchecked((int)entry.th32ParentProcessID), entry.szExeFile, path, started));
            }
            while (Process32NextW(snapshot, ref entry));

            return found;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    private static (string? Path, DateTimeOffset? StartedAt) Describe(int pid)
    {
        using SafeProcessHandle handle = OpenProcess(_queryLimitedInformation, false, unchecked((uint)pid));
        if (handle.IsInvalid)
        {
            return (null, null);
        }

        string? path = ImagePathOf(handle);
        DateTimeOffset? started = GetProcessTimes(handle, out long creation, out _, out _, out _)
            ? DateTimeOffset.FromFileTime(creation)
            : null;
        return (path, started);
    }

    private static string? ImagePathOf(SafeProcessHandle handle)
    {
        char[] buffer = new char[_maxPath];
        uint size = (uint)buffer.Length;
        if (!QueryFullProcessImageNameW(handle, 0, buffer, ref size) || size == 0)
        {
            return null;
        }

        string raw = new(buffer, 0, checked((int)size));
        try
        {
            return ExecutableIdentity.Normalise(raw);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public UIntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry32W entry);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry32W entry);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(SafeProcessHandle process, uint flags,
        [Out] char[] exeName, ref uint size);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long creation, out long exit,
        out long kernel, out long user);
}
