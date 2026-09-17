using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace FrameLedger.Infrastructure.Diagnostics;

/// <summary>
/// <c>10_LOGGING</c> §Crash handling (P4 PR-9): a minidump of the crashing process under <c>crashdumps\</c>, named
/// <c>&lt;process&gt;-&lt;UTC time&gt;-&lt;pid&gt;.dmp</c>, the five newest kept. Never throws: a crash path that throws is a
/// second crash.
/// </summary>
/// <remarks>
/// <b>Written by a child process since 2026-09-17</b> (<see cref="ParentDump"/> carries why): <see cref="TryWrite"/> starts
/// its dumper — the Agent binary, <c>--write-crash-dump &lt;file&gt;</c> — and waits for it to dump this
/// process from outside. Calling <c>MiniDumpWriteDump</c> on the calling process could deadlock it, and did, in the test
/// suite, one run in eight.
/// </remarks>
public static class CrashDumpWriter
{
    /// <summary>How many dumps a directory keeps.</summary>
    public const int Kept = 5;

    public const string Pattern = "*.dmp";

    /// <summary>The Agent flag that runs <see cref="ParentDump"/>.</summary>
    public const string DumperFlag = "--write-crash-dump";

    /// <summary>How long a crashing process waits for its dump. A dump of a busy process takes a second or two.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>The dump's path, or null — with one line to <paramref name="log"/> — when none was written.</summary>
    /// <param name="directory">Where dumps are kept.</param>
    /// <param name="process"><c>ui</c> or <c>agent</c>: the file name's prefix.</param>
    /// <param name="now">The crash's time; the name carries it in UTC.</param>
    /// <param name="dumper">The executable that dumps its parent: <c>FrameLedger.Agent.exe</c> beside the App, or the Agent itself.</param>
    /// <param name="log">One line per failure.</param>
    public static string? TryWrite(string directory, string process, DateTimeOffset now, string? dumper, Action<string>? log = null)
    {
        string? path = null;
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);
            ArgumentException.ThrowIfNullOrWhiteSpace(process);
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, string.Create(CultureInfo.InvariantCulture, $"{process}-{now.UtcDateTime:yyyyMMdd-HHmmss}-{Environment.ProcessId}.dmp"));
            if (dumper is null || !File.Exists(dumper))
            {
                log?.Invoke($"crash dump: not written — no dumper at {dumper ?? "(none)"}");
                return null;
            }

            var start = new ProcessStartInfo(dumper) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(DumperFlag);
            start.ArgumentList.Add(path);
            using Process helper = Process.Start(start) ?? throw new InvalidOperationException($"{dumper} did not start");
            if (!helper.WaitForExit(Timeout))
            {
                helper.Kill(entireProcessTree: true);
                log?.Invoke($"crash dump: not written — the dumper did not finish within {Timeout.TotalSeconds:0} s");
                TryDelete(path);
                return null;
            }

            if (helper.ExitCode != ParentDump.ExitWritten || !File.Exists(path) || new FileInfo(path).Length == 0)
            {
                log?.Invoke($"crash dump: not written — the dumper exited {helper.ExitCode}");
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

    /// <summary>Keeps the <see cref="Kept"/> newest <c>*.dmp</c> in <paramref name="directory"/>; touches nothing else.</summary>
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
