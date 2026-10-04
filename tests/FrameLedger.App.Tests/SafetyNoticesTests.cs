// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>
/// 08_UI §Notifications policy, "safety events are never toasts": a refusal, an unhook and a degrade become
/// persistent notices with the signal named and the acknowledge action; a capture error is the strip's; a
/// session event is nobody's; a notice leaves only when the user dismisses it.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the expected texts are resources that follow the UI culture")]
public sealed class SafetyNoticesTests
{
    [Fact]
    public void ARefusalNamesTheSignalAndOffersTheOneAction()
    {
        var link = new FakeAgentLink();
        var strip = new RecordingStrip();
        using var notices = new SafetyNotices(link, strip);

        link.Raise(IpcMessageType.CaptureRefused, new CaptureRefusedEvent(7, "Title", "GuardBlocked", "Easy Anti-Cheat", "EasyAntiCheat.sys"));

        SafetyNotice notice = notices.Items.Should().ContainSingle().Subject;
        notice.Kind.Should().Be(SafetyNoticeKind.Refused);
        notice.IsError.Should().BeTrue();
        notice.Title.Should().Contain("Title");
        notice.Body.Should().Contain("Easy Anti-Cheat").And.Contain(Strings.Notice_Refused_Recording);
        notice.ActionText.Should().Be(Shared.Strings.Safety_RecordWithoutMeasuring);
        strip.Shown.Should().BeEmpty("never a toast, never a snackbar");
    }

    /// <summary>
    /// 2026-09-21: a game whose process an anti-cheat driver protects (ELDEN RING under EAC) reached the user as a toast
    /// reading "InjectFailed: TargetAmbiguous" and no session. It is a persistent notice that says why. Since 2026-09-22 the
    /// session IS recorded — the loop holds it, unhooked, by the executable's name until the game exits — so the
    /// "still recorded" sentence is true here too and is said.
    /// </summary>
    [Fact]
    public void AProcessThatCannotBeOpenedIsAPersistentNoticeThatSaysWhyAndIsRecordedUnmeasured()
    {
        var link = new FakeAgentLink();
        var strip = new RecordingStrip();
        using var notices = new SafetyNotices(link, strip);

        link.Raise(IpcMessageType.CaptureRefused, new CaptureRefusedEvent(7, "ELDEN RING", "TargetUnreadable", null, null));

        SafetyNotice notice = notices.Items.Should().ContainSingle().Subject;
        notice.Kind.Should().Be(SafetyNoticeKind.Refused);
        notice.Title.Should().Contain("ELDEN RING");
        notice.Body.Should().StartWith(Shared.Strings.Safety_Refused_TargetUnreadable);
        notice.Body.Should().Contain(Strings.Notice_Refused_Recording);
        strip.Shown.Should().BeEmpty("never a toast");
    }

    /// <summary>
    /// §S23-3 (beta.13): while Easy Anti-Cheat's service runs for another title, EVERY game refuses with
    /// <c>BlockedService</c> (spike-notes §13), and the notice said it was detected "in this game" — a game the user may not
    /// even own. A driver or a service is said as running on this PC; a module in the game still says "in this game".
    /// </summary>
    [Fact]
    public void AServiceOrDriverIsOnThisPcAndAModuleIsInThisGame()
    {
        var link = new FakeAgentLink();
        using var notices = new SafetyNotices(link, new RecordingStrip());

        link.Raise(IpcMessageType.CaptureRefused, new CaptureRefusedEvent(7, "Title", "RefusedByGuard", "Easy Anti-Cheat", "EasyAntiCheat_EOS", GuardReason: "BlockedService"));
        link.Raise(IpcMessageType.CaptureRefused, new CaptureRefusedEvent(7, "Title", "RefusedByGuard", "Riot Vanguard", "vgk.sys", GuardReason: "BlockedDriver"));
        link.Raise(IpcMessageType.CaptureRefused, new CaptureRefusedEvent(7, "Title", "RefusedByGuard", "BattlEye", "BEClient_x64.dll", GuardReason: "BlockedModule"));
        link.Raise(IpcMessageType.CaptureRefused, new CaptureRefusedEvent(7, "Title", "RefusedByGuard", "BattlEye", "BEClient_x64.dll"));

        string[] bodies = [.. notices.Items.Reverse().Select(static n => n.Body)];
        bodies[0].Should().StartWith(Format(Shared.Strings.Safety_Refused_MachineWide_Format, "Easy Anti-Cheat", "EasyAntiCheat_EOS"));
        bodies[1].Should().StartWith(Format(Shared.Strings.Safety_Refused_MachineWide_Format, "Riot Vanguard", "vgk.sys"));
        bodies[2].Should().StartWith(Format(Shared.Strings.Safety_Refused_Named_Format, "BattlEye", "BEClient_x64.dll"));
        bodies[3].Should().StartWith(Format(Shared.Strings.Safety_Refused_Named_Format, "BattlEye", "BEClient_x64.dll"), "an older Agent sends no guard reason");
    }

    /// <summary>
    /// The unhook notice promised "will not inject into this game again until you enable it again", untrue both ways since
    /// 2026-09-22: a service that starts on the PC turns no hooking off, and a finding about the game turns it off for good —
    /// which the turned-off sentence then says (beta.13).
    /// </summary>
    [Fact]
    public void AnUnhookSaysWhereTheFindingWasAndPromisesNoReEnable()
    {
        var link = new FakeAgentLink();
        using var notices = new SafetyNotices(link, new RecordingStrip());

        link.Raise(IpcMessageType.SafetyUnhook, new SafetyUnhookEvent(Guid.NewGuid(), "Easy Anti-Cheat", "EasyAntiCheat_EOS", GuardReason: "BlockedService"));
        link.Raise(IpcMessageType.SafetyUnhook, new SafetyUnhookEvent(Guid.NewGuid(), "BattlEye", "BEClient_x64.dll", HookingTurnedOff: true, GuardReason: "BlockedModule"));

        string[] bodies = [.. notices.Items.Reverse().Select(static n => n.Body)];
        bodies[0].Should().Be(Format(Shared.Strings.Safety_Unhooked_MachineWide_Format, "Easy Anti-Cheat", "EasyAntiCheat_EOS"));
        bodies[1].Should().Be(Format(Shared.Strings.Safety_Unhooked_Format, "BattlEye") + " " + Shared.Strings.Safety_HookingTurnedOff);
    }

    [Fact]
    public void ACouldNotVerifyRefusalUsesItsOwnSentence()
    {
        var link = new FakeAgentLink();
        using var notices = new SafetyNotices(link, new RecordingStrip());

        link.Raise(IpcMessageType.CaptureRefused, new CaptureRefusedEvent(7, null, "PreScanCouldNotVerify", null, null));

        notices.Items.Should().ContainSingle().Which.Body.Should().StartWith(Shared.Strings.Safety_Refused_CouldNotVerify);
    }

    /// <summary>The finding turned the game's hooking off (2026-09-22): the notice says so, on a refusal and on an unhook alike.</summary>
    [Fact]
    public void WhenTheFindingTurnedHookingOffTheNoticeSaysSo()
    {
        var link = new FakeAgentLink();
        using var notices = new SafetyNotices(link, new RecordingStrip());

        link.Raise(IpcMessageType.CaptureRefused, new CaptureRefusedEvent(7, "Title", "RefusedByGuard", "Easy Anti-Cheat", "EasyAntiCheat.sys", HookingTurnedOff: true));
        link.Raise(IpcMessageType.SafetyUnhook, new SafetyUnhookEvent(Guid.NewGuid(), "BattlEye", "BEService.exe", HookingTurnedOff: true));
        link.Raise(IpcMessageType.CaptureRefused, new CaptureRefusedEvent(7, "Title", "RefusedByGuard", "Riot Vanguard", "vgk.sys"));

        notices.Items.Should().HaveCount(3);
        notices.Items[2].Body.Should().EndWith(Shared.Strings.Safety_HookingTurnedOff);
        notices.Items[1].Body.Should().EndWith(Shared.Strings.Safety_HookingTurnedOff);
        notices.Items[0].Body.Should().NotContain(Shared.Strings.Safety_HookingTurnedOff, "a machine-wide driver turns nothing off");
    }

    /// <summary>
    /// The Agent's pre-scan of the library turned hooking off for a game the user had turned it on for (beta.8): a
    /// persistent notice naming what was found and saying hooking is off — and none of a refusal's session sentence or
    /// its "record without measuring" action, because no session is running.
    /// </summary>
    [Fact]
    public void APreScanThatTurnedHookingOffIsAPersistentNoticeWithNoSessionInIt()
    {
        var link = new FakeAgentLink();
        var strip = new RecordingStrip();
        using var notices = new SafetyNotices(link, strip);

        link.Raise(IpcMessageType.HookingTurnedOff, new HookingTurnedOffEvent(7, "Title", "AntiCheatDirectory", "Easy Anti-Cheat", "EasyAntiCheat"));

        SafetyNotice notice = notices.Items.Should().ContainSingle().Subject;
        notice.Kind.Should().Be(SafetyNoticeKind.HookingOff);
        notice.IsError.Should().BeTrue();
        notice.Title.Should().Contain("Title");
        notice.Body.Should().Contain("Easy Anti-Cheat").And.EndWith(Shared.Strings.Safety_HookingTurnedOff);
        notice.Body.Should().NotContain(Strings.Notice_Refused_Recording, "no session is running");
        notice.ActionText.Should().Be(Strings.Notice_Dismiss);
        strip.Shown.Should().BeEmpty("never a toast");
    }

    [Fact]
    public void AnUnhookAndADegradeArePersistentToo()
    {
        var link = new FakeAgentLink();
        using var notices = new SafetyNotices(link, new RecordingStrip());

        link.Raise(IpcMessageType.SafetyUnhook, new SafetyUnhookEvent(Guid.NewGuid(), "BattlEye", "BEService.exe"));
        link.Raise(IpcMessageType.CaptureDegraded, new CaptureDegradedEvent(Guid.NewGuid(), 1, 2, "HookFaulted"));

        notices.Items.Should().HaveCount(2);
        notices.Items[0].Kind.Should().Be(SafetyNoticeKind.Degraded, "newest first");
        notices.Items[0].ActionText.Should().Be(Strings.Notice_Dismiss);
        notices.Items[1].Body.Should().Contain("BattlEye");
        notices.Items[1].IsError.Should().BeTrue();
        notices.Items[0].IsError.Should().BeFalse();
    }

    [Fact]
    public void ACaptureErrorIsTheStripsAndASessionEventIsNobodys()
    {
        var link = new FakeAgentLink();
        var strip = new RecordingStrip();
        using var notices = new SafetyNotices(link, strip);

        link.Raise(IpcMessageType.CaptureError, new CaptureErrorEvent(null, "RingLost", "the ring went away"));
        link.Raise(IpcMessageType.SessionStarted, new SessionStartedEvent(Guid.NewGuid(), 7, "Title", 1, 1, DateTimeOffset.UtcNow));

        notices.Items.Should().BeEmpty();
        strip.Shown.Should().ContainSingle().Which.Should().Be(("warn", Strings.Notice_Error_Title, "RingLost: the ring went away"));
    }

    [Fact]
    public void OnlyTheUserDismissesANotice()
    {
        var link = new FakeAgentLink();
        using var notices = new SafetyNotices(link, new RecordingStrip());
        link.Raise(IpcMessageType.CaptureRefused, new CaptureRefusedEvent(7, "Title", "GuardBlocked", null, null));
        SafetyNotice notice = notices.Items[0];

        notices.Dismiss(notice);

        notices.Items.Should().BeEmpty();
    }

    private static string Format(string format, string first) => string.Format(CultureInfo.CurrentCulture, format, first);

    private static string Format(string format, string first, string second) => string.Format(CultureInfo.CurrentCulture, format, first, second);
}
