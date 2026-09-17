using FluentAssertions;
using FrameLedger.Infrastructure.Diagnostics;

namespace FrameLedger.Infrastructure.Tests.Diagnostics;

/// <summary>
/// <c>10_LOGGING</c> §Crash handling's minidump (P4 PR-9): a real <c>MiniDumpWriteDump</c> of this test process under the
/// directory, named in the invariant culture; the newest five kept and nothing else touched; a directory that cannot be
/// used is a null path and a line, never an exception on a crash path.
/// </summary>
public sealed class CrashDumpWriterTests : IDisposable
{
    private static readonly string[] _fiveNewest = ["ui-0.dmp", "ui-1.dmp", "ui-2.dmp", "ui-3.dmp", "ui-4.dmp"];

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-dumps-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    /// <summary>
    /// 2026-09-17: this class used to dump ITS OWN process, and MiniDumpWriteDump on the calling process suspends every other
    /// thread of it — one run in eight, a suspended test thread held a lock the dumper needed and the whole suite froze
    /// (it cancelled the first tag's release run). The dump is written by a child process now; this is the writer's side of
    /// that contract with a dumper that fails, and <see cref="ParentDumpTests"/> dumps a real child process.
    /// </summary>
    [Fact]
    public void ADumperThatFailsOrIsMissingLeavesNoFileAndOneLine()
    {
        var lines = new List<string>();
        string where = Path.Combine(Environment.SystemDirectory, "where.exe");

        CrashDumpWriter.TryWrite(_dir, "test", DateTimeOffset.UtcNow, where, lines.Add).Should().BeNull("where.exe does not know the flag and exits non-zero");
        lines.Should().ContainSingle().Which.Should().StartWith("crash dump: not written — the dumper exited");
        Directory.EnumerateFiles(_dir, "*.dmp").Should().BeEmpty();

        lines.Clear();
        CrashDumpWriter.TryWrite(_dir, "test", DateTimeOffset.UtcNow, Path.Combine(_dir, "absent.exe"), lines.Add).Should().BeNull();
        lines.Should().ContainSingle().Which.Should().Contain("no dumper");
    }

    [Fact]
    public void PruneKeepsTheFiveNewestDumpsAndNothingElseIsTouched()
    {
        Directory.CreateDirectory(_dir);
        DateTime now = DateTime.UtcNow;
        for (int i = 0; i < CrashDumpWriter.Kept + 2; i++)
        {
            string dump = Path.Combine(_dir, $"ui-{i}.dmp");
            File.WriteAllText(dump, "MDMP");
            File.SetLastWriteTimeUtc(dump, now.AddMinutes(-i));
        }

        string notes = Path.Combine(_dir, "notes.txt");
        File.WriteAllText(notes, "not a dump");
        File.SetLastWriteTimeUtc(notes, now.AddDays(-30));

        CrashDumpWriter.Prune(_dir);

        Directory.EnumerateFiles(_dir, "*.dmp").Select(Path.GetFileName).Should().BeEquivalentTo(_fiveNewest);
        File.Exists(notes).Should().BeTrue("the prune reads *.dmp only");
    }

    [Fact]
    public void ADirectoryThatCannotBeUsedIsNullAndALineNotAnException()
    {
        Directory.CreateDirectory(_dir);
        string occupied = Path.Combine(_dir, "crashdumps");
        File.WriteAllText(occupied, "a file where the directory should be");
        var lines = new List<string>();

        string? path = CrashDumpWriter.TryWrite(occupied, "ui", DateTimeOffset.UtcNow, dumper: null, lines.Add);

        path.Should().BeNull();
        lines.Should().ContainSingle().Which.Should().StartWith("crash dump: not written");
        CrashDumpWriter.Prune(Path.Combine(_dir, "absent"));
    }
}
