using FluentAssertions;
using FrameLedger.Application.Recording;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>
/// The words of <c>capture_notes</c>, read back so a row can say why it ended and, for Tier 2, why it measured nothing
/// (2026-09-22) — the three-slot guard word since beta.8, and the rows written before it.
/// </summary>
public sealed class CaptureNotesTests
{
    [Fact]
    public void TheThreeSlotGuardWordReadsBackWhateverIsEmpty()
    {
        CaptureNotes n = CaptureNotes.Parse("end=RefusedByGuard; tier2: attach=NotEvaluated; guard=BlockedModule|Easy Anti-Cheat|EasyAntiCheat_EOS.dll");
        (n.End, n.GuardReason, n.GuardFamily, n.GuardSignal).Should().Be(("RefusedByGuard", "BlockedModule", "Easy Anti-Cheat", "EasyAntiCheat_EOS.dll"));

        CaptureNotes familyless = CaptureNotes.Parse("end=RefusedByGuard; guard=ProcessUnreadable||Access is denied, twice");
        (familyless.GuardReason, familyless.GuardFamily, familyless.GuardSignal).Should().Be(("ProcessUnreadable", (string?)null, "Access is denied, twice"));

        CaptureNotes blocked = CaptureNotes.Parse("end=RefusedHookNotEnabled; guard=PreviouslyBlocked||AntiCheatDirectory|Easy Anti-Cheat|EasyAntiCheat");
        blocked.GuardSignal.Should().Be("AntiCheatDirectory|Easy Anti-Cheat|EasyAntiCheat", "the signal is the last slot and keeps its own bars");
    }

    [Fact]
    public void TheRecorderWritesEverySlotAndNoSemicolonInsideOne()
    {
        CaptureNotes.GuardSlot("PreScanFailed", null, "could not list; access denied").Should().Be("guard=PreScanFailed||could not list, access denied");
        CaptureNotes round = CaptureNotes.Parse("end=RefusedByGuard; " + CaptureNotes.GuardSlot("PreScanFailed", "", "could not list; access denied"));
        (round.GuardReason, round.GuardFamily, round.GuardSignal).Should().Be(("PreScanFailed", (string?)null, "could not list, access denied"));
    }

    /// <summary>
    /// A row from before beta.8: '/' between the parts and an empty family dropped, so a familyless reason's second part was its
    /// signal. A finding names a family and the gate's own refusals carry a label there — that is how the two are told apart.
    /// </summary>
    [Fact]
    public void ARowFromBeforeReadsTheSecondPartAsTheSignalUnlessTheReasonNamesAFamily()
    {
        CaptureNotes unreadable = CaptureNotes.Parse("end=RefusedByGuard; tier2: attach=NotEvaluated; guard=ProcessUnreadable/Access is denied");
        (unreadable.GuardReason, unreadable.GuardFamily, unreadable.GuardSignal).Should().Be(("ProcessUnreadable", (string?)null, "Access is denied"),
            "until beta.8 this read as a family called 'Access is denied'");

        CaptureNotes bare = CaptureNotes.Parse("end=RefusedHookNotEnabled; tier2: attach=NotEvaluated; guard=HookNotEnabled/not enabled");
        (bare.GuardFamily, bare.GuardSignal).Should().Be(("not enabled", (string?)null), "the gate's own label sits in the family slot");

        CaptureNotes finding = CaptureNotes.Parse("end=RefusedByGuard; guard=BlockedDriver/Riot Vanguard");
        (finding.GuardFamily, finding.GuardSignal).Should().Be(("Riot Vanguard", (string?)null));

        CaptureNotes path = CaptureNotes.Parse("end=SafetyUnhook; guard=AntiCheatFile/BattlEye/C:/Games/BEService/BEService.exe");
        path.GuardSignal.Should().Be("C:/Games/BEService/BEService.exe", "only the first two slashes split");

        CaptureNotes none = CaptureNotes.Parse(null);
        none.End.Should().BeNull();
        CaptureNotes hooked = CaptureNotes.Parse("end=Running");
        (hooked.End, hooked.GuardReason).Should().Be(("Running", null));
    }

    [Fact]
    public void TheExitCodeTheWitnessAndTheLaunchErrorAreReadBack()
    {
        CaptureNotes n = CaptureNotes.Parse("end=TargetExited; exit_code=-1073741819; crash_event=application_log");
        (n.ExitCode, n.CrashEvent).Should().Be((-1073741819, true));

        CaptureNotes launch = CaptureNotes.Parse("end=LaunchCannotStart; launch_error=740");
        (launch.LaunchError, launch.ExitCode, launch.CrashEvent).Should().Be((740, (int?)null, false));
    }
}
