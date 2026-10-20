// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.App.Services;
using FrameLedger.App.ViewModels;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Tests;

/// <summary>
/// The live card's two kinds of session (2026-09-23): a hooked one reads its readout; one held without measuring reads its
/// name, its tier, its elapsed time and why — never a readout. Since beta.28 a card is one session's, and another session's
/// events leave it alone. In the culture collection because it compares against the resources it reads.
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
        card.Hold(new SessionHeldEvent(guid, 65, GpuTempC: 61.4, CpuTempC: 55.2, GameVramDedicatedMb: 2048, GameRamPrivateMb: 1536));
        card.ElapsedText.Should().Contain(Formats.Duration(65));
        card.GpuTempText.Should().Contain("61");
        card.CpuTempText.Should().Contain("55");
        card.MemoryText.Should().Be(GameMemoryText.Live(2048, 1536), "a held session has the game's memory too: it is read from outside the game (beta.12)");

        card.Stop(new SessionCompletedEvent(guid, 3, "normal", 2, "saved", "TargetUnreadable"));

        card.IsActive.Should().BeFalse();
        card.IsHeld.Should().BeFalse();
        card.GameName.Should().BeEmpty("until 2026-09-23 the name stayed, and the next notice could name the last game");
        card.HeldReason.Should().BeEmpty();
        card.ElapsedText.Should().BeEmpty();
        card.MemoryText.Should().BeEmpty();
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

    /// <summary>beta.28: the Dashboard keeps a card per session, so a card never changes hands — until then a hooked session took the one card from a held one.</summary>
    [Fact]
    public void ACardIsOneSessionsAndAnotherSessionsEventsLeaveItAlone()
    {
        var guid = Guid.NewGuid();
        var card = new LiveCaptureViewModel(guid);

        card.Start(Started(Guid.NewGuid(), 1, "Another"));
        card.IsActive.Should().BeFalse("another session's start is that session's card's");

        card.Start(Started(guid, 2, "Held", new SessionHold("RefusedHookNotEnabled")));
        card.Start(Started(Guid.NewGuid(), 1, "Hooked elsewhere"));
        card.Seed(new RunningSession(Guid.NewGuid(), 9, "Another hooked", 1, DateTimeOffset.UnixEpoch, Hold: null));
        card.Update(new SessionProgressEvent { SessionGuid = Guid.NewGuid(), ElapsedS = 5, Presents5s = 10, PresentedQualifier = "census_not_run", FgMode = "na" });

        card.SessionGuid.Should().Be(guid);
        card.GameName.Should().Be("Held");
        card.IsHeld.Should().BeTrue();

        card.Start(Started(guid, 1, "Held"));
        card.Tier.Should().Be(1, "its own session attached");
        card.IsHeld.Should().BeFalse();
    }
}
