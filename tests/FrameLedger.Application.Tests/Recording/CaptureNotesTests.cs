using FluentAssertions;
using FrameLedger.Application.Recording;

namespace FrameLedger.Application.Tests.Recording;

/// <summary>The words of <c>capture_notes</c>, read back so a Tier-2 row can say why (2026-09-22).</summary>
public sealed class CaptureNotesTests
{
    [Fact]
    public void TheEndAndTheGuardsWordsAreReadBack()
    {
        CaptureNotes n = CaptureNotes.Parse("end=RefusedByGuard; tier2: attach=NotEvaluated; guard=BlockedModule/Easy Anti-Cheat/EasyAntiCheat_EOS.dll");

        n.End.Should().Be("RefusedByGuard");
        n.GuardReason.Should().Be("BlockedModule");
        n.GuardFamily.Should().Be("Easy Anti-Cheat");
        n.GuardSignal.Should().Be("EasyAntiCheat_EOS.dll");
    }

    [Fact]
    public void AGuardWordWithoutAFamilyOrSignalReadsAsNullsAndASignalMayCarrySlashes()
    {
        CaptureNotes bare = CaptureNotes.Parse("end=RefusedHookNotEnabled; tier2: attach=NotEvaluated; guard=HookNotEnabled/not enabled");
        bare.GuardReason.Should().Be("HookNotEnabled");
        bare.GuardFamily.Should().Be("not enabled");
        bare.GuardSignal.Should().BeNull();

        CaptureNotes path = CaptureNotes.Parse("end=SafetyUnhook; guard=AntiCheatFile/BattlEye/C:/Games/BEService/BEService.exe");
        path.GuardSignal.Should().Be("C:/Games/BEService/BEService.exe", "only the first two slashes split");

        CaptureNotes none = CaptureNotes.Parse(null);
        none.End.Should().BeNull();
        CaptureNotes hooked = CaptureNotes.Parse("end=Running");
        (hooked.End, hooked.GuardReason).Should().Be(("Running", null));
    }
}
