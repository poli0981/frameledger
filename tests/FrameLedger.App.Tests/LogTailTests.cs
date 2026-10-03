// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.IO;
using System.Text;
using FluentAssertions;
using FrameLedger.App.Services;

namespace FrameLedger.App.Tests;

/// <summary><c>10_LOGGING</c> §In-app log viewer: the newest file, the 2 MB cap with shared read, the level and text filters.</summary>
public sealed class LogTailTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-logs-" + Guid.NewGuid().ToString("N"));

    public LogTailTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void TheNewestFileOfEachSourceIsPicked()
    {
        var tail = new LogTail(_dir);
        tail.NewestFile(LogSource.Ui).Should().BeNull();

        Write("ui-20260901.log", "[00:00:00.000 INF] old\n", DateTime.UtcNow.AddDays(-2));
        Write("ui-20260914.log", "[00:00:00.000 INF] new\n", DateTime.UtcNow);
        Write("agent-20260914.log", "2026-09-14 00:00:00.000 +07:00 [INF] agent\n", DateTime.UtcNow);

        Path.GetFileName(tail.NewestFile(LogSource.Ui)).Should().Be("ui-20260914.log");
        Path.GetFileName(tail.NewestFile(LogSource.Agent)).Should().Be("agent-20260914.log");
    }

    [Fact]
    public void AFileHeldOpenByItsWriterIsStillReadAndCappedAtTheTail()
    {
        string path = Path.Combine(_dir, "ui-20260914.log");
        using (var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        {
            byte[] line = Encoding.UTF8.GetBytes("[12:00:00.000 INF] 0123456789012345678901234567890123456789\n");
            for (int i = 0; i < LogTail.MaxBytes / line.Length + 100; i++)
            {
                writer.Write(line, 0, line.Length);
            }

            writer.Flush();

            IReadOnlyList<string> lines = LogTail.ReadLines(path);

            lines.Should().NotBeEmpty();
            lines.Sum(static l => l.Length + 1).Should().BeLessThanOrEqualTo(LogTail.MaxBytes);
            lines.Should().OnlyContain(static l => l.StartsWith("[12:00:00.000 INF]", StringComparison.Ordinal), "the partial first line is dropped");
        }
    }

    [Fact]
    public void TheLevelFilterReadsTheTemplateAndKeepsNothingElse()
    {
        string[] lines =
        [
            "[12:00:00.000 INF] fine",
            "[12:00:00.001 WRN] hmm",
            "[12:00:00.002 ERR] bad",
            "   at Some.Stack()",
            "[12:00:00.003 FTL] worst",
            "[12:00:00.004 DBG] noise",
        ];

        LogTail.Filter(lines, LogLevelFilter.All, null).Should().HaveCount(6);
        LogTail.Filter(lines, LogLevelFilter.WarningAndAbove, null).Should().BeEquivalentTo(["[12:00:00.001 WRN] hmm", "[12:00:00.002 ERR] bad", "[12:00:00.003 FTL] worst"]);
        LogTail.Filter(lines, LogLevelFilter.ErrorAndAbove, "worst").Should().ContainSingle().Which.Should().EndWith("worst");
        LogTail.Filter(lines, LogLevelFilter.All, "STACK").Should().ContainSingle("the search is case-insensitive");
        LogTail.LevelOf("   at Some.Stack()").Should().BeNull();
        LogTail.LevelOf("[12:00:00.000 INF] x").Should().Be("INF");
    }

    /// <summary>
    /// The Agent writes Serilog's default template — a date, then the level in brackets — and until 2026-10-04 the filter
    /// read only the App's, so Warning and above showed no Agent line at all (this class's fake agent log was written in
    /// the App's template, which is how that went unseen).
    /// </summary>
    [Fact]
    public void TheAgentsTemplateIsReadToo()
    {
        string[] agent =
        [
            "2026-10-04 09:15:02.123 +07:00 [INF] watcher: started",
            "2026-10-04 09:15:03.456 +07:00 [WRN] guard: refused Game.exe",
            "System.InvalidOperationException: boom",
            "   at Some.Stack()",
            "2026-10-04 09:15:04.789 -05:00 [ERR] recorder: finalize failed",
            "2026-10-04 is not a log line [WRN]",
        ];

        LogTail.LevelOf(agent[0]).Should().Be("INF");
        LogTail.LevelOf(agent[1]).Should().Be("WRN");
        LogTail.LevelOf(agent[2]).Should().BeNull();
        LogTail.LevelOf(agent[5]).Should().BeNull("a bracket that is not the level's place is not a level");
        LogTail.Filter(agent, LogLevelFilter.WarningAndAbove, null).Should().Equal(agent[1], agent[4]);
        LogTail.Filter(agent, LogLevelFilter.ErrorAndAbove, null).Should().Equal(agent[4]);
    }

    private void Write(string name, string text, DateTime mtimeUtc)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, text);
        File.SetLastWriteTimeUtc(path, mtimeUtc);
    }
}
