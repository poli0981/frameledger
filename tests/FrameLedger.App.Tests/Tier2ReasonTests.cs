using System.Globalization;
using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>Tier 2's payload on the summary (2026-09-22): why the session measured nothing, from the row's notes.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the expected texts are resources that follow the UI culture")]
public sealed class Tier2ReasonTests
{
    [Fact]
    public void EachEndReasonHasItsSentence()
    {
        Formats.Tier2Reason("end=RefusedHookNotEnabled; tier2: attach=NotEvaluated; guard=HookNotEnabled/not enabled/hooking is off")
            .Should().Be(Strings.Summary_Tier2_Why_HookOff);
        Formats.Tier2Reason("end=RefusedHookNotEnabled; tier2: attach=NotEvaluated; guard=PreviouslyBlocked/previously blocked/BlockedModule: Easy Anti-Cheat EasyAntiCheat_EOS.dll")
            .Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.Summary_Tier2_Why_Blocked_Format, "BlockedModule: Easy Anti-Cheat EasyAntiCheat_EOS.dll"));
        Formats.Tier2Reason("end=RefusedByGuard; tier2: attach=NotEvaluated; guard=BlockedDriver/Riot Vanguard/vgk.sys")
            .Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.Summary_Tier2_Why_Guard_Format, "Riot Vanguard", "vgk.sys"));
        Formats.Tier2Reason("end=TargetUnreadable; tier2: attach=NotEvaluated").Should().Be(Strings.Summary_Tier2_Why_Unreadable);
        Formats.Tier2Reason("end=AttachRefused; tier2: attach=BuildIdMismatch").Should().Be(Strings.End_AttachRefused, "every end reason has its own words since beta.8");
        Formats.Tier2Reason("end=SomethingLater; tier2: attach=NotEvaluated")
            .Should().Be(string.Format(CultureInfo.CurrentCulture, Strings.Summary_Tier2_Why_Other_Format, "SomethingLater"), "a name this build does not know is named");
        Formats.Tier2Reason(null).Should().BeEmpty();
        Formats.Tier2Reason("end=RefusedConsentMissing; tier2: attach=NotEvaluated; guard=ConsentMissing/consent/stale")
            .Should().Be(Strings.Summary_Tier2_Why_ConsentChanged, "a store update changes the executable the consent was given for");
    }

    /// <summary>The live card says what the summary will (2026-09-23): a running session's hold reads through the same sentences as its row's notes.</summary>
    [Fact]
    public void ARunningHoldReadsLikeTheRowItWillBecome()
    {
        Formats.Tier2HoldReason(new SessionHold("RefusedHookNotEnabled", "PreviouslyBlocked", "previously blocked", "BlockedModule: Easy Anti-Cheat EasyAntiCheat_EOS.dll"))
            .Should().Be(Formats.Tier2Reason("end=RefusedHookNotEnabled; tier2: attach=NotEvaluated; guard=PreviouslyBlocked/previously blocked/BlockedModule: Easy Anti-Cheat EasyAntiCheat_EOS.dll"));
        Formats.Tier2HoldReason(new SessionHold("RefusedByGuard", "BlockedDriver", "Riot Vanguard", "vgk.sys"))
            .Should().Be(Formats.Tier2Reason("end=RefusedByGuard; tier2: attach=NotEvaluated; guard=BlockedDriver/Riot Vanguard/vgk.sys"));
        Formats.Tier2HoldReason(new SessionHold("RefusedHookNotEnabled", "HookNotEnabled", "not enabled", "hooking is off")).Should().Be(Strings.Summary_Tier2_Why_HookOff);
        Formats.Tier2HoldReason(new SessionHold("TargetUnreadable")).Should().Be(Strings.Summary_Tier2_Why_Unreadable);
    }
}
