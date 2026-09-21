using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>
/// 08_UI §Notifications policy, "safety events are never toasts": a refusal, an unhook and a degrade become
/// persistent notices with the signal named and the acknowledge action; a capture error is the strip's; a
/// session event is nobody's; a notice leaves only when the user dismisses it.
/// </summary>
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
    /// 2026-09-22: a game whose process an anti-cheat driver protects (ELDEN RING under EAC, with the guard bypass on)
    /// reached the user as a toast reading "InjectFailed: TargetAmbiguous" and no session. It is a persistent notice that
    /// says why and that the bypass does not change it - and it does NOT claim the session is still recorded, because
    /// that run ends at once and is discarded.
    /// </summary>
    [Fact]
    public void AProcessThatCannotBeOpenedIsAPersistentNoticeThatSaysWhyAndPromisesNoRecording()
    {
        var link = new FakeAgentLink();
        var strip = new RecordingStrip();
        using var notices = new SafetyNotices(link, strip);

        link.Raise(IpcMessageType.CaptureRefused, new CaptureRefusedEvent(7, "ELDEN RING", "TargetUnreadable", null, null));

        SafetyNotice notice = notices.Items.Should().ContainSingle().Subject;
        notice.Kind.Should().Be(SafetyNoticeKind.Refused);
        notice.Title.Should().Contain("ELDEN RING");
        notice.Body.Should().Be(Shared.Strings.Safety_Refused_TargetUnreadable);
        notice.Body.Should().NotContain(Strings.Notice_Refused_Recording);
        strip.Shown.Should().BeEmpty("never a toast");
    }

    [Fact]
    public void ACouldNotVerifyRefusalUsesItsOwnSentence()
    {
        var link = new FakeAgentLink();
        using var notices = new SafetyNotices(link, new RecordingStrip());

        link.Raise(IpcMessageType.CaptureRefused, new CaptureRefusedEvent(7, null, "PreScanCouldNotVerify", null, null));

        notices.Items.Should().ContainSingle().Which.Body.Should().StartWith(Shared.Strings.Safety_Refused_CouldNotVerify);
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
}
