using System.Diagnostics;
using FluentAssertions;
using FrameLedger.Application.Watch;
using FrameLedger.Infrastructure.Diagnostics;

namespace FrameLedger.Infrastructure.Tests.Diagnostics;

/// <summary>
/// The dumping half of the crash-dump contract (2026-09-17): a minidump of ANOTHER process, and the rule for which process
/// the helper will read — its parent, still running, started before it, from the helper's own directory, never a pid.
/// </summary>
public sealed class ParentDumpTests : IDisposable
{
    private static readonly DateTimeOffset _t0 = new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-parentdump-" + Guid.NewGuid().ToString("N"));

    public ParentDumpTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void AnotherProcessIsDumpedFromOutsideAndTheFileIsAMinidump()
    {
        // A child that just waits: dumping it cannot suspend THIS process, which is the whole point.
        using Process child = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "ping.exe"), "-n 30 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        string file = Path.Combine(_dir, "child.dmp");
        var problems = new List<string>();
        try
        {
            // A process started microseconds ago is still being mapped by the loader, and MiniDumpWriteDump then fails
            // with ERROR_PARTIAL_COPY (0x8007012B) - seen on CI 2026-09-21. The product never dumps a process that young
            // (it dumps one that crashed), so the TEST waits for the child to finish loading: retry, bounded at 5 s.
            bool written = false;
            for (int attempt = 0; attempt < 50 && !written; attempt++)
            {
                problems.Clear();
                written = ParentDump.WriteDump(child, file, problems.Add);
                if (!written)
                {
                    Thread.Sleep(100);
                }
            }

            written.Should().BeTrue(string.Join(" | ", problems));
        }
        finally
        {
            child.Kill();
            child.WaitForExit(10_000);
        }

        byte[] head = new byte[4];
        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            stream.ReadExactly(head);
        }

        head.Should().Equal("MDMP"u8.ToArray(), "a minidump starts with its signature");
        problems.Should().BeEmpty();
    }

    [Fact]
    public void AFailedDumpLeavesNoFileAndNamesTheErrorDbghelpReturned()
    {
        // A process that has exited has nothing to read, so the dump fails. The line must carry dbghelp's code: CsWin32
        // declares MiniDumpWriteDump without SetLastError, and GetLastPInvokeError read 0.
        using Process child = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/c exit 0") { UseShellExecute = false, CreateNoWindow = true })!;
        child.WaitForExit(10_000).Should().BeTrue();
        string file = Path.Combine(_dir, "exited.dmp");
        var problems = new List<string>();

        ParentDump.WriteDump(child, file, problems.Add).Should().BeFalse("an exited process cannot be dumped");

        File.Exists(file).Should().BeFalse();
        problems.Should().ContainSingle().Which.Should().StartWith("crash dump: MiniDumpWriteDump failed (0x").And.NotContain("0x00000000");
    }

    [Fact]
    public void OnlyAParentFromTheHelpersOwnDirectoryThatStartedFirstIsOurs()
    {
        string ours = Path.Combine(_dir, "app");
        var helper = new ProcessSnapshot(200, 100, "FrameLedger.Agent.exe", Path.Combine(ours, "FrameLedger.Agent.exe"), _t0.AddSeconds(5));

        ParentDump.IsOurs(new ProcessSnapshot(100, 1, "FrameLedger.exe", Path.Combine(ours, "FrameLedger.exe"), _t0), helper, ours)
            .Should().BeTrue("the App beside the helper, started before it");
        ParentDump.IsOurs(new ProcessSnapshot(100, 1, "FrameLedger.exe", Path.Combine(ours, "FrameLedger.exe"), _t0), helper, ours + Path.DirectorySeparatorChar)
            .Should().BeTrue("a trailing separator is the same directory");
        ParentDump.IsOurs(new ProcessSnapshot(100, 1, "Game.exe", @"D:\Games\Game\Game.exe", _t0), helper, ours)
            .Should().BeFalse("a game is never installed beside FrameLedger — CLAUDE.md rule 4");
        ParentDump.IsOurs(new ProcessSnapshot(100, 1, "FrameLedger.exe", Path.Combine(ours, "FrameLedger.exe"), _t0.AddSeconds(10)), helper, ours)
            .Should().BeFalse("a 'parent' that started after the helper is a recycled pid, not the parent");
        ParentDump.IsOurs(new ProcessSnapshot(100, 1, "FrameLedger.exe", null, _t0), helper, ours)
            .Should().BeFalse("an image nobody could read is not proven ours");
        ParentDump.IsOurs(new ProcessSnapshot(100, 1, "FrameLedger.exe", Path.Combine(ours, "FrameLedger.exe"), null), helper, ours)
            .Should().BeFalse();
    }
}
