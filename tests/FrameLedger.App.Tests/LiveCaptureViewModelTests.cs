using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>
/// The live card's two kinds of session (2026-09-23): a hooked one reads its readout; one held without measuring reads its
/// name, its tier, its elapsed time and why — never a readout — and a hooked session takes the card from it, never the
/// reverse. In the culture collection because it compares against the resources it reads.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class LiveCaptureViewModelTests
{
    private static SessionStartedEvent Started(Guid guid, int tier, string name, SessionHold? hold = null) => new(guid, 7, name, 42, tier, DateTimeOffset.UnixEpoch, hold);

    [Fact]
    public void AHeldSessionShowsWhyAndNoReadoutAndItsEndClearsEverything()
    {
        var card = new LiveCaptureViewModel();
        var guid = Guid.NewGuid();

        card.Start(Started(guid, 2, "Held", new SessionHold("TargetUnreadable")));

        card.IsActive.Should().BeTrue();
        card.IsHeld.Should().BeTrue();
        card.WaitingForFrames.Should().BeFalse("nothing will ever arrive to wait for");
        card.TierText.Should().Be(Strings.Tier_NotHooked);
        card.HeldReason.Should().Be(Strings.Summary_Tier2_Why_Unreadable, "the sentence its summary will carry");
        card.Readout.Should().BeSameAs(FpsReadoutModel.Unavailable);

        card.Hold(new SessionHeldEvent(Guid.NewGuid(), 99, GpuTempC: 80));
        card.ElapsedText.Should().BeEmpty("another session's tick");
        card.Hold(new SessionHeldEvent(guid, 65, GpuTempC: 61.4, CpuTempC: 55.2));
        card.ElapsedText.Should().Contain(Formats.Duration(65));
        card.GpuTempText.Should().Contain("61");
        card.CpuTempText.Should().Contain("55");

        card.Stop(new SessionCompletedEvent(guid, 3, "normal", 2, "saved", "TargetUnreadable"));

        card.IsActive.Should().BeFalse();
        card.IsHeld.Should().BeFalse();
        card.GameName.Should().BeEmpty("until 2026-09-23 the name stayed, and the next notice could name the last game");
        card.HeldReason.Should().BeEmpty();
        card.ElapsedText.Should().BeEmpty();
        card.Tier.Should().Be(0);
    }

    [Fact]
    public void ASessionThatHasNotAttachedYetIsStartingNotHeld()
    {
        var card = new LiveCaptureViewModel();
        var guid = Guid.NewGuid();

        card.Seed(new RunningSession(guid, 7, "Starting", 2, DateTimeOffset.UnixEpoch, Hold: null));

        card.IsHeld.Should().BeFalse("the status lists a session at tier 2 until its attach; only a hold says nothing is measured");
        card.WaitingForFrames.Should().BeFalse("the waiting line says \"Attached\", and it is not yet");
        card.HeldReason.Should().BeEmpty();
        card.GameName.Should().Be("Starting");

        card.Start(Started(guid, 1, "Starting"));
        card.Tier.Should().Be(1);
        card.TierText.Should().Be(Strings.Tier_Hooked);
        card.WaitingForFrames.Should().BeTrue();
    }

    [Fact]
    public void AHookedSessionTakesTheCardFromAHeldOneNeverTheReverse()
    {
        var card = new LiveCaptureViewModel();
        var hooked = Guid.NewGuid();

        card.Start(Started(Guid.NewGuid(), 2, "Held", new SessionHold("RefusedHookNotEnabled")));
        card.Start(Started(hooked, 1, "Hooked"));
        card.SessionGuid.Should().Be(hooked);
        card.IsHeld.Should().BeFalse();

        card.Start(Started(Guid.NewGuid(), 2, "Another held", new SessionHold("RefusedHookNotEnabled")));
        card.Seed(new RunningSession(Guid.NewGuid(), 9, "Another hooked", 1, DateTimeOffset.UnixEpoch, Hold: null));

        card.SessionGuid.Should().Be(hooked, "the card keeps a hooked session until it ends");
        card.GameName.Should().Be("Hooked");
    }
}
