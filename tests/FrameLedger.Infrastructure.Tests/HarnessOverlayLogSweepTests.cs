using FluentAssertions;
using FrameLedger.Testing;

namespace FrameLedger.Infrastructure.Tests;

/// <summary>
/// The assembly-end sweep of the hook-harness's overlay logs (<c>17_HOOK_ENGINE</c> §Native logging): it removes this
/// assembly's harness logs from the run, and never a game's log, another assembly's harness log, or an earlier run's.
/// </summary>
public sealed class HarnessOverlayLogSweepTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fl-sweep-" + Guid.NewGuid().ToString("N"));

    public HarnessOverlayLogSweepTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void RemovesThisAssemblysHarnessLogsFromTheRunAndNothingElse()
    {
        DateTime since = DateTime.UtcNow.AddSeconds(-1);
        string bin = Path.Combine(_dir, "bin");
        string copies = Path.Combine(_dir, "copies");
        string[] owned = [bin, copies];
        string ours = Plant("overlay-101-20260915-010101.log", Path.Combine(bin, "hook-harness.exe"));
        string ourCopy = Plant("overlay-102-20260915-010101.log", Path.Combine(copies, "HOOK-HARNESS-abc.exe"));
        string otherAssembly = Plant("overlay-103-20260915-010101.log", @"C:\elsewhere\bin\hook-harness.exe");
        string game = Plant("overlay-104-20260915-010101.log", Path.Combine(bin, "title.exe"));
        string earlier = Plant("overlay-105-19990101-000000.log", Path.Combine(bin, "hook-harness.exe"));
        File.SetCreationTimeUtc(earlier, new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        string stranger = Path.Combine(_dir, "overlay-106-20260915-010101.log");
        File.WriteAllText(stranger, "# a file that is not an overlay log\r\n");
        string agent = Path.Combine(_dir, "agent-20260915.log");
        File.WriteAllText(agent, Header(Path.Combine(bin, "hook-harness.exe")));

        (int removed, int kept) = HarnessOverlayLogSweep.Sweep(_dir, since, image => HarnessOverlayLogSweep.IsHarnessUnder(image, owned));

        removed.Should().Be(2);
        kept.Should().Be(3, "another assembly's harness, a game and a stranger were created in the window and left");
        File.Exists(ours).Should().BeFalse();
        File.Exists(ourCopy).Should().BeFalse("a copy under a registered directory is ours, whatever its case");
        File.Exists(otherAssembly).Should().BeTrue("a parallel assembly may still read its own log");
        File.Exists(game).Should().BeTrue("a game's log is never a test's to remove");
        File.Exists(earlier).Should().BeTrue("a log from an earlier run is the owner's");
        File.Exists(stranger).Should().BeTrue();
        File.Exists(agent).Should().BeTrue("only overlay-*.log is read");
    }

    [Fact]
    public void TheHeaderImageIsReadFromTheOverlaysOwnFirstLine()
    {
        HarnessOverlayLogSweep.OverlayLogImage(@"# FrameLedger.Overlay build 1a2b pid 42 layout v9 image C:\x\hook-harness.exe" + "\r").Should().Be(@"C:\x\hook-harness.exe");
        HarnessOverlayLogSweep.OverlayLogImage("# a stale log planted by guard_test").Should().BeNull();
        HarnessOverlayLogSweep.OverlayLogImage(null).Should().BeNull();
        HarnessOverlayLogSweep.IsHarnessUnder(@"C:\x\bin\hook-harness.exe", [@"C:/x/bin/"]).Should().BeTrue("separators and a trailing one do not matter");
        HarnessOverlayLogSweep.IsHarnessUnder(@"C:\x\binary\hook-harness.exe", [@"C:\x\bin"]).Should().BeFalse("a sibling directory is not under it");
        HarnessOverlayLogSweep.IsHarnessUnder(@"C:\x\bin\game.exe", [@"C:\x\bin"]).Should().BeFalse();
        HarnessOverlayLogSweep.RealLogsDirectory.Should().EndWith(Path.Combine("FrameLedger", "logs"));
    }

    private static string Header(string image) => "# FrameLedger.Overlay build test pid 101 layout v9 image " + image + "\n";

    private string Plant(string name, string image)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, Header(image) + "RING_CREATED\n");
        return path;
    }
}
