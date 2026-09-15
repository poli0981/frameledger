using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Diagnostics.Debug;

namespace FrameLedger.Infrastructure.Diagnostics;

/// <summary>
/// <c>10_LOGGING</c> §Crash handling: on a fatal exception, a minidump of this process through <c>MiniDumpWriteDump</c>
/// (<c>MiniDumpWithIndirectlyReferencedMemory | MiniDumpWithThreadInfo</c>) into <c>crashdumps\</c>, the newest five kept
/// (P4 PR-9). Called from a crash path, so it never throws: every failure is a null path and a line for the caller's log.
/// The dump stays on this machine; a bug bundle carries it only when the user says so (<c>legal/PRIVACY_POLICY.md</c> §3).
/// </summary>
public static class CrashDumpWriter
{
    /// <summary>How many dumps the directory keeps; the oldest beyond this go after each write.</summary>
    public const int Kept = 5;

    /// <summary>The dump file pattern; <see cref="Prune"/> and the bug bundle read nothing else from the directory.</summary>
    public const string Pattern = "*.dmp";

    /// <summary>
    /// Writes <c>&lt;process&gt;-&lt;yyyyMMdd-HHmmss&gt;-&lt;pid&gt;.dmp</c> (UTC) under <paramref name="directory"/> and prunes to
    /// <see cref="Kept"/>. Returns the path, or null when nothing usable was written (the partial file is removed).
    /// </summary>
    public static unsafe string? TryWrite(string directory, string process, DateTimeOffset now, Action<string>? log = null)
    {
        string? path = null;
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);
            ArgumentException.ThrowIfNullOrWhiteSpace(process);
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, string.Create(CultureInfo.InvariantCulture, $"{process}-{now.UtcDateTime:yyyyMMdd-HHmmss}-{Environment.ProcessId}.dmp"));
            bool written;
            using (var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (Process self = Process.GetCurrentProcess())
            {
                written = PInvoke.MiniDumpWriteDump(
                    new HANDLE(self.Handle),
                    (uint)self.Id,
                    new HANDLE(file.SafeFileHandle.DangerousGetHandle()),
                    MINIDUMP_TYPE.MiniDumpWithIndirectlyReferencedMemory | MINIDUMP_TYPE.MiniDumpWithThreadInfo,
                    null,
                    null,
                    null);
            }

            if (!written)
            {
                log?.Invoke($"crash dump: MiniDumpWriteDump failed (Win32 error {Marshal.GetLastPInvokeError()})");
                TryDelete(path);
                return null;
            }

            Prune(directory, log);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            log?.Invoke($"crash dump: not written ({ex.GetType().Name}: {ex.Message})");
            if (path is not null)
            {
                TryDelete(path);
            }

            return null;
        }
    }

    /// <summary>Keeps the <see cref="Kept"/> newest dumps in <paramref name="directory"/>; a file that cannot be removed stays.</summary>
    public static void Prune(string directory, Action<string>? log = null)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (FileInfo old in new DirectoryInfo(directory).EnumerateFiles(Pattern).OrderByDescending(static f => f.LastWriteTimeUtc).Skip(Kept))
        {
            try
            {
                old.Delete();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log?.Invoke($"crash dump: could not prune {old.Name} ({ex.Message})");
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing more a crash path can do; the next prune gets another chance.
        }
    }
}
