using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Tests;

/// <summary>
/// The bug report's optional crash dump (P4 PR-9): the box starts clear (<c>legal/PRIVACY_POLICY.md</c> §3, only when
/// ticked), closing the dialog never includes it, and the label carries the dump's size.
/// </summary>
public sealed class BugBundleOptionsViewModelTests
{
    [Fact]
    public void TheBoxStartsClearAndOnlyATickAndContinueIncludesTheDump()
    {
        var vm = new BugBundleOptionsViewModel(new CrashDumpInfo(@"C:\data\crashdumps\ui-1.dmp", new DateTimeOffset(2026, 9, 15, 1, 2, 3, TimeSpan.Zero), 1536 * 1024));

        vm.IncludeCrashDump.Should().BeFalse();
        vm.Choice(continued: true).Should().Be(CrashDumpChoice.LeaveOut);
        vm.Choice(continued: false).Should().Be(CrashDumpChoice.Cancel);
        vm.IncludeCrashDump = true;
        vm.Choice(continued: true).Should().Be(CrashDumpChoice.Include);
        vm.Choice(continued: false).Should().Be(CrashDumpChoice.Cancel, "closing the dialog never includes the dump");
        vm.DumpLabel.Should().Contain(string.Format(CultureInfo.CurrentCulture, "{0:0.0}", 1.5), "the size is in the label, in MB");
    }
}
