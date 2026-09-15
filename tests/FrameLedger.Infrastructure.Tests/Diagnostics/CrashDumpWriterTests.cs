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

    [Fact]
    public void WritesAMinidumpOfThisProcessNamedByProcessUtcTimeAndPid()
    {
        var lines = new List<string>();
        var now = new DateTimeOffset(2026, 9, 15, 1, 2, 3, TimeSpan.FromHours(7));

        string? path = CrashDumpWriter.TryWrite(_dir, "test", now, lines.Add);

        path.Should().NotBeNull("MiniDumpWriteDump of our own process succeeds: {0}", string.Join(" | ", lines));
        Path.GetFileName(path).Should().Be($"test-20260914-180203-{Environment.ProcessId}.dmp", "the name carries the UTC time");
        Path.GetDirectoryName(path).Should().Be(_dir);
        byte[] head = new byte[4];
        using (var file = new FileStream(path!, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            file.ReadExactly(head);
        }

        head.Should().Equal("MDMP"u8.ToArray(), "a minidump starts with its signature");
        lines.Should().BeEmpty();
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

        string? path = CrashDumpWriter.TryWrite(occupied, "ui", DateTimeOffset.UtcNow, lines.Add);

        path.Should().BeNull();
        lines.Should().ContainSingle().Which.Should().StartWith("crash dump: not written");
        CrashDumpWriter.Prune(Path.Combine(_dir, "absent"));
    }
}
