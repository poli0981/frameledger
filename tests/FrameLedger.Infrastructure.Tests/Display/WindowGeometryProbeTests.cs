using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;
using FrameLedger.Domain.Display;
using FrameLedger.Infrastructure.Display;

namespace FrameLedger.Infrastructure.Tests.Display;

/// <summary>
/// The window reader against a real window in another process (beta.10, <c>03_METRICS</c> §Display mode):
/// <c>hook-harness --hold-presenting-hwnd</c> makes a top-level window with a swap chain on it and prints the handle. Nothing
/// is injected — the reader asks user32 about a window from outside, as the Agent does.
/// </summary>
/// <remarks>
/// The harness's window is never shown (a hosted runner has nothing to show it on), so the positive case goes through the
/// NAMED handle, the path region 4 feeds; the largest-visible-window search is exercised by what it must not find. A
/// machine with no window station or no monitor skips, and says so.
/// </remarks>
public sealed partial class WindowGeometryProbeTests
{
    private static string Harness => Path.Combine(AppContext.BaseDirectory, "hook-harness.exe");

    [GeneratedRegex(@"hwnd=0x(?<hwnd>[0-9a-fA-F]+) width=(?<w>\d+) height=(?<h>\d+)", RegexOptions.None, 1000)]
    private static partial Regex FixtureLine();

    [Fact]
    public async Task TheNamedWindowIsReadWhenItsOwnerIsAmongThePidsAndRefusedWhenNot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        File.Exists(Harness).Should().BeTrue("hook-harness.exe must be staged beside the test binary (FrameLedger.DrainFixtures.targets); run build.ps1 native first");
        using Process harness = Process.Start(new ProcessStartInfo(Harness, "--hold-presenting-hwnd 15")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        })!;
        try
        {
            Match? fixture = null;
            while (fixture is null && await harness.StandardOutput.ReadLineAsync(ct).ConfigureAwait(true) is { } line)
            {
                Assert.SkipWhen(line.Contains("[SKIP]", StringComparison.Ordinal), "the harness could not make a window with a swap chain here: " + line.Trim());
                Match m = FixtureLine().Match(line);
                fixture = m.Success ? m : null;
            }

            fixture.Should().NotBeNull("the harness prints the window it made before it starts presenting");
            ulong hwnd = ulong.Parse(fixture!.Groups["hwnd"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var probe = new WindowGeometryProbe();

            WindowView? view = probe.Observe([harness.Id], hwnd);

            Assert.SkipWhen(view is null, "no monitor to place the window on in this session");
            view!.Value.Minimized.Should().BeFalse();
            view.Value.Client.IsEmpty.Should().BeFalse("the harness's window has a client area");
            view.Value.Monitor.IsEmpty.Should().BeFalse();
            view.Value.CoversMonitor.Should().BeFalse("a 320×200 window covers no monitor");
            DisplayModeClassifier.Classify(null, view).Mode.Should().Be(DisplayMode.Windowed);

            // The same handle asked about a process that does not own it: a handle is reused once its window is gone, so
            // one that belongs to someone else is not the game's — and this test process shows no window of its own.
            probe.Observe([Environment.ProcessId], hwnd).Should().BeNull();
            probe.Observe([], hwnd).Should().BeNull("no process, no window");
        }
        finally
        {
            try
            {
                if (!harness.HasExited)
                {
                    harness.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // Exited between the check and the kill.
            }
        }
    }

    [Fact]
    public void AHandleThatIsNoWindowIsNotRead() =>
        new WindowGeometryProbe().Observe([Environment.ProcessId], 0xDEAD_0000_BEEF).Should().BeNull();
}
