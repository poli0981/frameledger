using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;

namespace FrameLedger.App.Tests;

/// <summary>
/// The bug report's optional items: every box starts clear (<c>legal/PRIVACY_POLICY.md</c> §3, only when ticked), only an
/// item that exists can be included, closing the dialog includes nothing, and each label says what and how big.
/// </summary>
public sealed class BugBundleOptionsViewModelTests
{
    private static readonly CrashDumpInfo _dump = new(@"C:\data\crashdumps\ui-1.dmp", new DateTimeOffset(2026, 9, 15, 1, 2, 3, TimeSpan.Zero), 1536 * 1024);
    private static readonly LastSessionInfo _session = new(42, "Title", new DateTimeOffset(2026, 9, 15, 3, 4, 5, TimeSpan.Zero));

    [Fact]
    public void EveryBoxStartsClearAndOnlyATickAndContinueIncludesAnItem()
    {
        var vm = new BugBundleOptionsViewModel(new BugBundleOffer(_dump, _session));

        vm.IncludeCrashDump.Should().BeFalse();
        vm.IncludeLastSession.Should().BeFalse();
        vm.Options(continued: true).Should().Be(BugBundleOptions.NothingTicked);
        vm.Options(continued: false).Should().Be(BugBundleOptions.Cancel);
        vm.IncludeCrashDump = true;
        vm.Options(continued: true).Should().Be(new BugBundleOptions(Cancelled: false, IncludeCrashDump: true, IncludeLastSession: false));
        vm.IncludeLastSession = true;
        vm.Options(continued: true).Should().Be(new BugBundleOptions(Cancelled: false, IncludeCrashDump: true, IncludeLastSession: true));
        vm.Options(continued: false).Should().Be(BugBundleOptions.Cancel, "closing the dialog never includes anything");
        vm.DumpLabel.Should().Contain(string.Format(CultureInfo.CurrentCulture, "{0:0.0}", 1.5), "the size is in the label, in MB");
        vm.SessionLabel.Should().Contain("Title");
    }

    [Fact]
    public void AnItemThereIsNoneOfHasNoBoxAndCannotBeIncluded()
    {
        var vm = new BugBundleOptionsViewModel(new BugBundleOffer(CrashDump: null, _session));

        vm.HasCrashDump.Should().BeFalse();
        vm.HasLastSession.Should().BeTrue();
        vm.DumpLabel.Should().BeEmpty();
        vm.IncludeCrashDump = true;
        vm.Options(continued: true).IncludeCrashDump.Should().BeFalse("there is no dump to include");
        new BugBundleOffer(null, null).IsEmpty.Should().BeTrue();
    }
}
