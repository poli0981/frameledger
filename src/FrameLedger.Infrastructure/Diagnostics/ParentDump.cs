using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FrameLedger.Application.Watch;
using FrameLedger.Infrastructure.Watch;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Diagnostics.Debug;

namespace FrameLedger.Infrastructure.Diagnostics;

/// <summary>
/// The dumping half of <see cref="CrashDumpWriter"/>: runs in a CHILD process (<c>FrameLedger.Agent.exe --write-crash-dump
/// &lt;file&gt;</c>) and writes a minidump of its PARENT — the App or Agent that just crashed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a child (2026-09-17).</b> <c>MiniDumpWriteDump</c> on the calling process suspends every other thread of it and
/// then allocates and walks modules; a suspended thread holding the process heap or loader lock deadlocks the dumper
/// forever. Microsoft's documentation says to call it from a separate process "if at all possible". The in-process
/// version hung <c>FrameLedger.Infrastructure.Tests</c> about one run in eight — the test that dumped its own process froze
/// the twelve tests running beside it, output stopped after "Starting:", and it cancelled the first tag's release run
/// after 58 minutes. A crashing App or Agent is exactly the unstable process the documentation warns about, so the same
/// deadlock was waiting for a real crash. The installed runtime does not ship <c>createdump.exe</c> beside a self-contained
/// app, so the child is our own Agent binary.
/// </para>
/// <para>
/// <b>It dumps its parent and nothing else.</b> No pid is accepted — CLAUDE.md rule 4 forbids reading a game's memory and
/// §S27's lesson is that a verb naming a pid is how a tool gets aimed somewhere it should not go. The parent must still be
/// running, must have started before this process (a recycled pid is not our parent), and its image must sit in THIS
/// binary's own directory: the App and the Agent, never a game, which is never installed there.
/// </para>
/// </remarks>
public static class ParentDump
{
    /// <summary>Written.</summary>
    public const int ExitWritten = 0;

    /// <summary>The parent is gone, or is not a FrameLedger binary from this directory: nothing was read.</summary>
    public const int ExitRefused = 8;

    /// <summary>The dump could not be written.</summary>
    public const int ExitFailed = 9;

    /// <summary>The same content the in-process writer produced: the referenced memory and per-thread information.</summary>
    private const MINIDUMP_TYPE _kind = MINIDUMP_TYPE.MiniDumpWithIndirectlyReferencedMemory | MINIDUMP_TYPE.MiniDumpWithThreadInfo;

    /// <summary>Dumps this process's parent into <paramref name="file"/>; the exit code the helper returns.</summary>
    public static int Run(string file, Action<string> problem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        ArgumentNullException.ThrowIfNull(problem);

        using Process self = Process.GetCurrentProcess();
        IReadOnlyList<ProcessSnapshot> snapshot = new ToolhelpProcessSnapshotSource().Take();
        ProcessSnapshot? me = snapshot.FirstOrDefault(p => p.Pid == self.Id);
        ProcessSnapshot? parent = me is { } m ? snapshot.FirstOrDefault(p => p.Pid == m.ParentPid) : null;
        if (me is not { } child || parent is not { } candidate || !IsOurs(candidate, child, AppContext.BaseDirectory))
        {
            problem($"crash dump: refused — the parent is gone or is not a FrameLedger binary from {AppContext.BaseDirectory}");
            return ExitRefused;
        }

        try
        {
            using Process target = Process.GetProcessById(candidate.Pid);
            return WriteDump(target, file, problem) ? ExitWritten : ExitFailed;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            problem($"crash dump: the parent could not be opened ({ex.GetType().Name}: {ex.Message})");
            return ExitRefused;
        }
    }

    /// <summary>
    /// The only process this helper will read: still running, started before the helper (not a recycled pid), and whose
    /// image is in <paramref name="helperDirectory"/>.
    /// </summary>
    public static bool IsOurs(ProcessSnapshot parent, ProcessSnapshot helper, string helperDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(helperDirectory);
        if (parent.ImagePath is not { Length: > 0 } image || parent.StartedAt is not { } parentStarted || helper.StartedAt is not { } helperStarted)
        {
            return false;
        }

        string imageDirectory = Path.GetFullPath(Path.GetDirectoryName(image) ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar);
        string ours = Path.GetFullPath(helperDirectory).TrimEnd(Path.DirectorySeparatorChar);
        return parentStarted <= helperStarted && string.Equals(imageDirectory, ours, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Writes a minidump of <paramref name="target"/> — another process — into <paramref name="file"/>.</summary>
    public static unsafe bool WriteDump(Process target, string file, Action<string> problem)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        ArgumentNullException.ThrowIfNull(problem);

        bool written;
        int error;
        using (var stream = new FileStream(file, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            written = PInvoke.MiniDumpWriteDump(new HANDLE(target.Handle), (uint)target.Id, new HANDLE(stream.SafeFileHandle.DangerousGetHandle()), _kind, null, null, null);
            // At once, and the SYSTEM error: CsWin32 0.3.298 declares MiniDumpWriteDump without SetLastError, so
            // GetLastPInvokeError() is whatever an earlier call left (0 when measured on CreateNamedPipeW, 2026-09-17). The
            // value is an HRESULT, which is how dbghelp reports this function's failures.
            error = Marshal.GetLastSystemError();
        }

        if (!written)
        {
            problem($"crash dump: MiniDumpWriteDump failed (0x{error:X8})");
            File.Delete(file);
        }

        return written;
    }
}
