using System.Globalization;
using System.IO;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Application.Capture;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Sessions;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>
/// Clearer reasons (beta.8, owner item 5): every way a session can end and every refusal no anti-cheat family names has
/// its own words — the enums are walked so none falls through to a raw name — a crash names its exception while End
/// task's exit code is an ordinary end, a launch says why it could not start, and a missing executable says whether its
/// drive or the file is gone.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class ReasonTextTests
{
    private static T InEnglish<T>(Func<T> f)
    {
        CultureInfo? previous = Strings.Culture;
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            Strings.Culture = CultureInfo.GetCultureInfo("en");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
            return f();
        }
        finally
        {
            Strings.Culture = previous;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void EveryEndReasonHasItsOwnSentence()
    {
        foreach (SessionEndReason reason in Enum.GetValues<SessionEndReason>())
        {
            Formats.EndReasonText(reason.ToString()).Should().NotBeNullOrWhiteSpace($"{reason} has no words, so a row would print its name");
        }

        Formats.EndReasonText("SomethingLater").Should().BeNull();
    }

    /// <summary>Every reason the guard can give without naming an anti-cheat family has a clause of its own.</summary>
    [Fact]
    public void EveryFamilylessGuardReasonHasItsOwnWords() => InEnglish(() =>
    {
        foreach (AntiCheatRefusalReason reason in Enum.GetValues<AntiCheatRefusalReason>())
        {
            // The findings name their family, and the kill switch always carries its own label and end reason.
            if (reason is AntiCheatRefusalReason.Allow or AntiCheatRefusalReason.BlockedModule or AntiCheatRefusalReason.BlockedDriver
                or AntiCheatRefusalReason.BlockedService or AntiCheatRefusalReason.BlockedExecutable or AntiCheatRefusalReason.BlockedStoreId
                or AntiCheatRefusalReason.AntiCheatDirectory or AntiCheatRefusalReason.AntiCheatFile or AntiCheatRefusalReason.KillSwitchEngaged)
            {
                continue;
            }

            Formats.GuardReasonText(reason.ToString()).Should().NotStartWith("reason ", $"{reason} falls through to its raw name");
        }

        return 0;
    });

    [Fact]
    public void AFamilylessRefusalIsSaidAsWhatItIsNeverAsDetected() => InEnglish(() =>
    {
        Formats.Tier2Reason("end=RefusedByGuard; tier2: attach=NotEvaluated; guard=TargetIsWow64||target is a WOW64 process")
            .Should().Be("The anti-cheat guard did not hook this game: the game is 32-bit, and the hook runs in 64-bit games only.");
        Formats.Tier2Reason("end=RefusedByGuard; tier2: attach=NotEvaluated; guard=ProcessUnreadable/Access is denied")
            .Should().Be("The anti-cheat guard did not hook this game: the game's process could not be opened.", "a row from before beta.8 reads the same");

        var link = new FakeAgentLink();
        using var notices = new SafetyNotices(link, new RecordingStrip());
        link.Raise(IpcMessageType.CaptureRefused, new CaptureRefusedEvent(7, "Title", "PreScanFailed", null, "the folder could not be listed"));
        notices.Items.Should().ContainSingle().Which.Body.Should().StartWith("The anti-cheat guard did not hook this game: the game's folder could not be scanned.")
            .And.NotContain("detected");
        new HookingConsentResult(HookingConsentOutcome.Refused, Refusal: new RefusedAck(7, "ProcessUnreadable", null, "Access is denied")).RefusalText()
            .Should().Be("The anti-cheat guard did not hook this game: the game's process could not be opened.");
        return 0;
    });

    [Fact]
    public void ACrashNamesItsExceptionAndEndTasksCodeIsAnOrdinaryEnd() => InEnglish(() =>
    {
        Formats.ExitText(ExitStatus.Crashed, CaptureNotes.Parse("end=TargetExited; exit_code=-1073741819")).Should().Be("crashed (0xC0000005 access violation)");
        Formats.ExitText(ExitStatus.Crashed, CaptureNotes.Parse("end=TargetExited; exit_code=-1073741818"))
            .Should().Contain("0xC0000006 in-page I/O error", "the owner's failing USB drive, 2026-09-21");
        Formats.ExitText(ExitStatus.Crashed, CaptureNotes.Parse("end=TargetExited; exit_code=0; crash_event=application_log"))
            .Should().Be("crashed (Windows logged an application error)");
        Formats.ExitText(ExitStatus.Normal, CaptureNotes.Parse("end=TargetExited; exit_code=1")).Should().StartWith("ended with exit code 1 — what End task and taskkill leave");
        Formats.ExitText(ExitStatus.Normal, CaptureNotes.Parse("end=TargetExited; exit_code=0")).Should().Be("ended normally");
        Formats.ExitText(ExitStatus.Normal, CaptureNotes.Parse("end=TargetExited; exit_code=-1")).Should().Be("ended with exit code -1");
        Formats.ExitText(ExitStatus.Normal, CaptureNotes.Parse("end=TargetExited; exit_code=-1073741510")).Should().Be("ended with exit code 0xC000013A");
        Formats.ExitText(ExitStatus.UnhookedSafety, default).Should().Be(Formats.ExitStatusText(ExitStatus.UnhookedSafety));
        return 0;
    });

    [Fact]
    public void ALaunchThatCouldNotStartSaysWhy() => InEnglish(() =>
    {
        Formats.Tier2Reason("end=LaunchCannotStart; launch_error=740").Should().Be("FrameLedger could not start the game. (the game requires administrator rights)");
        Formats.LaunchErrorText(2).Should().Be("the file was not found");
        Formats.LaunchErrorText(1392).Should().Be("Windows error 1392");
        Formats.Tier2Reason("end=LaunchCannotStart").Should().Be("FrameLedger could not start the game.", "a row from before says what it can");
        return 0;
    });

    [Fact]
    public void AMissingExecutableSaysWhetherItsDriveOrTheFileIsGone() => InEnglish(() =>
    {
        string missingDrive = FreeDriveLetter() + @"Games\Title\game.exe";
        Formats.ExecutableMissingText(missingDrive).Should().StartWith("The drive " + missingDrive[..3] + " is not connected.");
        string gone = Path.Combine(Path.GetTempPath(), "fl-gone-" + Guid.NewGuid().ToString("N") + ".exe");
        Formats.ExecutableMissingText(gone).Should().Be("The game's executable is no longer at " + gone + ".");
        return 0;
    });

    private static string FreeDriveLetter()
    {
        for (char c = 'Z'; c >= 'D'; c--)
        {
            string root = c + @":\";
            if (!Directory.Exists(root))
            {
                return root;
            }
        }

        throw new InvalidOperationException("every drive letter is taken");
    }
}
