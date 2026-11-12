// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Advanced;
using FrameLedger.Application.Capture;
using FrameLedger.Shared;

namespace FrameLedger.Application.Tests.Capture;

/// <summary>
/// What a session makes of region 5 (Advanced metrics train B): the facts of what the game told its upscaler, the two
/// series, and - the half that matters - that a zero, a "none" and an unread byte are never turned into a value.
/// </summary>
public sealed class InGameAdvancedSamplerTests
{
    private static AdvancedFact Fact(InGameAdvancedSampler sampler, string id) =>
        sampler.Facts.Should().ContainSingle(f => f.Id == id, id).Subject;

    private static string[] Rows(AdvancedFact fact) => [.. fact.Value.Rows!.Select(static r => r.Name + "=" + r.Value)];

    [Fact]
    public void NothingPublishedIsNothingRead()
    {
        var sampler = new InGameAdvancedSampler();

        sampler.Take(published: null).Should().BeNull();
        sampler.Take(new InGameAdvanced(InGameStates.Empty())).Should().BeNull("a publication with no group seen carries no reading");

        sampler.Facts.Should().BeEmpty();
        sampler.SawAnything.Should().BeFalse();
        sampler.Status(hooked: true).Should().Be(AdvancedInGameStatus.NothingSeen, "measured inside the game, and the game made none of the calls");
        sampler.Status(hooked: false).Should().Be(AdvancedInGameStatus.NotHooked);
    }

    [Fact]
    public void TheDlssOptionsBecomeTheSessionsFactsInTheVendorsOwnWords()
    {
        var sampler = new InGameAdvancedSampler();

        sampler.Take(new InGameAdvanced(InGameStates.Dlss())).Should().BeNull("the DLSS options are facts, not a series");

        Fact(sampler, "sl.dlss.output_size").Value.Text.Should().Be("2560 × 1440");
        Rows(Fact(sampler, "sl.dlss.presets")).Should().Equal(
            "DLAA=K", "Quality=J", "Balanced=Default", "Performance=M", "Ultra Performance=L", "Ultra Quality=Default");
        Fact(sampler, "sl.dlss.hdr_input").Value.Number.Should().Be(0, "the game said its colour buffers are not HDR");
        Fact(sampler, "sl.dlss.auto_exposure").Value.Number.Should().Be(1);
        Fact(sampler, "sl.dlss.alpha_upscaling").Value.Number.Should().Be(1);
        Rows(Fact(sampler, "sl.dlss.exposure")).Should().Equal("preExposure=1.5", "exposureScale=0.75");
        sampler.Facts.Should().OnlyContain(static f => f.Id.StartsWith("sl.dlss.", StringComparison.Ordinal), "no FSR call was seen");
        sampler.Status(hooked: true).Should().Be(AdvancedInGameStatus.Reading);
    }

    [Fact]
    public void AZeroANoneAndAnUnreadByteAreNeverAValue()
    {
        // 07_IPC §Region 5: zero is "not read". A version-1 DLSSOptions has no auto-exposure member; a game that left
        // its output size unset sent the vendor's sentinel, which the Overlay turns into 0; a Boolean has a third state.
        FlAdvancedState s = InGameStates.WithPresets(
            InGameStates.Empty() with
            {
                ValidMask = FlAdvancedValid.DlssOptions,
                DlssStructVersion = 1,
                DlssOutputWidth = 0,
                DlssOutputHeight = 1440,
                DlssColorBuffersHdr = 2,
                DlssAutoExposure = 0,       // the struct ended before it
                DlssAlphaUpscaling = 3,     // not a value the Overlay ever writes
            },
            12, 0, 0, 0, 0, 0);
        var sampler = new InGameAdvancedSampler();

        sampler.Take(new InGameAdvanced(s));

        sampler.Facts.Select(static f => f.Id).Should().Equal("sl.dlss.presets", "sl.dlss.hdr_input");
        Rows(Fact(sampler, "sl.dlss.presets")).Should().Equal("DLAA=K");
        Fact(sampler, "sl.dlss.hdr_input").Value.Number.Should().Be(1);
    }

    [Fact]
    public void TheFsrDispatchBecomesRangesAndLargestMagnitudesNeverASeriesOfWhereThePlayerLooked()
    {
        var sampler = new InGameAdvancedSampler();

        sampler.Take(new InGameAdvanced(InGameStates.Ffx()));

        AdvancedFact sharpening = Fact(sampler, "ffx.upscale.sharpening");
        Rows(sharpening).Should().Equal("enableSharpening=on", "sharpness=0.35");
        sharpening.Value.Rows![0].Token.Should().Be("on", "the App translates the word");
        Rows(Fact(sampler, "ffx.upscale.jitter")).Should().Equal("jitterOffset.x=≤ 0.3", "jitterOffset.y=≤ 0.4");
        Fact(sampler, "ffx.upscale.exposure").Value.Text.Should().Be("1.25");
        // D114: the field of view is the session's range, lowest to highest.
        Rows(Fact(sampler, "ffx.upscale.depth")).Should().Equal(
            "cameraNear=0.1", "cameraFar=1000", "cameraFovAngleVertical=0.9 – 1.1 rad (51.6° – 63°)");
        Fact(sampler, "ffx.upscale.flags").Value.Tokens.Should().Equal("non_linear_color_pq");
        sampler.Facts.Should().NotContain(static f => f.Id.StartsWith("sl.", StringComparison.Ordinal));
    }

    [Fact]
    public void AnFsrDepthPlaneAtInfinityIsShownAsInfiniteAndInNoOtherFieldIsInfinityAValue()
    {
        // Owner decision D119: an engine with an infinite depth hands FSR FLT_MAX for one plane, and the Overlay
        // stores that as +infinity's own bits. Lies of P's shape: the far plane a small number, the near one at
        // infinity.
        var sampler = new InGameAdvancedSampler();
        sampler.Take(new InGameAdvanced(InGameStates.Ffx() with
        {
            FfxNearBits = FlAdvancedState.FloatInfinite,
            FfxFarBits = BitConverter.SingleToUInt32Bits(10f),
        }));

        Rows(Fact(sampler, "ffx.upscale.depth")).Should().StartWith(["cameraNear=∞", "cameraFar=10"]);

        // The other way round, in the same session: the fact follows what the game says now.
        sampler.Take(new InGameAdvanced(InGameStates.Ffx(2) with
        {
            FfxNearBits = BitConverter.SingleToUInt32Bits(0.05f),
            FfxFarBits = FlAdvancedState.FloatInfinite,
        }));
        Rows(Fact(sampler, "ffx.upscale.depth")).Should().StartWith(["cameraNear=0.05", "cameraFar=∞"]);

        // Infinity is a depth plane's value and nobody else's: an exposure or a jitter carrying the pattern is, as
        // before, no value - the Overlay never stores it there, and a reader does not invent a meaning for it.
        var other = new InGameAdvancedSampler();
        other.Take(new InGameAdvanced(InGameStates.Ffx() with
        {
            FfxPreExposureMinBits = FlAdvancedState.FloatInfinite,
            FfxPreExposureMaxBits = FlAdvancedState.FloatInfinite,
            FfxJitterMaxXBits = FlAdvancedState.FloatInfinite,
            FfxJitterMaxYBits = FlAdvancedState.FloatInfinite,
            FfxNearBits = FlAdvancedState.FloatNone,
            FfxFarBits = 0xFF800000u,
        }));
        other.Facts.Should().NotContain(static f => f.Id == "ffx.upscale.exposure" || f.Id == "ffx.upscale.jitter");
        Rows(Fact(other, "ffx.upscale.depth")).Should().OnlyContain(static row => row.StartsWith("cameraFov", StringComparison.Ordinal),
            "a plane nobody gave and a negative infinity are not planes");
    }

    [Fact]
    public void SharpeningSeenBothWaysARangeAndAFlagNobodyNamedAreSaidAsTheyAre()
    {
        FlAdvancedState s = InGameStates.Ffx() with
        {
            FfxSharpenSeen = 3,
            FfxSharpnessMinBits = InGameStates.Bits(0.2f),
            FfxPreExposureMaxBits = InGameStates.Bits(2f),
            FfxFovMaxBits = InGameStates.Bits(0.9f),
            FfxFlagsSeen = (1u << 0) | (1u << 5),
        };
        var sampler = new InGameAdvancedSampler();

        sampler.Take(new InGameAdvanced(s));

        Rows(Fact(sampler, "ffx.upscale.sharpening")).Should().Equal("enableSharpening=on_and_off", "sharpness=0.2 – 0.35");
        InGameAdvancedSampler.Tokens.Should().Contain(["on", "off", "on_and_off", "none", "not_radians", "draw_debug_view", "non_linear_color_srgb", "non_linear_color_pq"],
            "each is a word the App has in three languages; a test there holds it to this list");
        Fact(sampler, "ffx.upscale.exposure").Value.Text.Should().Be("1.25 – 2");
        Rows(Fact(sampler, "ffx.upscale.depth")).Should().Contain("cameraFovAngleVertical=0.9 rad (51.6°)", "one value is said once");
        Fact(sampler, "ffx.upscale.flags").Value.Tokens.Should().Equal("draw_debug_view", "bit_5");
    }

    [Fact]
    public void AnFsrDispatchThatSaidNothingMoreStillSaysItSetNoFlag()
    {
        FlAdvancedState s = InGameStates.Empty() with { ValidMask = FlAdvancedValid.FfxUpscale, FfxDispatches = 10 };
        var sampler = new InGameAdvancedSampler();

        sampler.Take(new InGameAdvanced(s)).Should().BeNull("no frame time was reported and nobody asked for the memory");

        sampler.Facts.Should().ContainSingle().Which.Value.Tokens.Should().Equal("none");
    }

    [Fact]
    public void TheFrameTimeIsTheMeanOfOneSecondAndTheMemoryAPointWhereTheGameAsked()
    {
        var sampler = new InGameAdvancedSampler();

        IReadOnlyList<AdvancedReading> first = sampler.Take(new InGameAdvanced(InGameStates.Ffx(samples: 1)))!;
        first.Should().Equal(
            new AdvancedReading(InGameAdvancedSampler.FrameTimeSeries, 16.0),
            new AdvancedReading(InGameAdvancedSampler.MemorySeries, 256.0));

        // The next second: dispatches went on, the game did not ask again - the memory is not repeated.
        FlAdvancedState second = InGameStates.Ffx(samples: 2) with { FfxMemoryQueries = 60, FfxFrameTimeCount = 30, FfxFrameTimeSumUs = 30 * 33_000 };
        sampler.Take(new InGameAdvanced(second)).Should().Equal(new AdvancedReading(InGameAdvancedSampler.FrameTimeSeries, 33.0));

        // A second with no dispatch, and one whose sum saturated its word: neither is a mean of anything.
        sampler.Take(new InGameAdvanced(InGameStates.Ffx(samples: 3) with { FfxMemoryQueries = 60, FfxFrameTimeCount = 0, FfxFrameTimeSumUs = 0 })).Should().BeNull();
        sampler.Take(new InGameAdvanced(InGameStates.Ffx(samples: 4) with { FfxMemoryQueries = 60, FfxFrameTimeSumUs = uint.MaxValue })).Should().BeNull();
    }

    [Fact]
    public void TheSamePublicationIsTakenOnce()
    {
        var sampler = new InGameAdvancedSampler();
        var published = new InGameAdvanced(InGameStates.Ffx());

        sampler.Take(published).Should().NotBeNull();
        sampler.Take(published).Should().BeNull("the loop hands the same instance over ten times a second");
    }

    [Fact]
    public void TheFactsAreTheSameListUntilSomethingInThemMoves()
    {
        // The recorder writes its context when a reference changes; a publication arrives every second.
        var sampler = new InGameAdvancedSampler();
        sampler.Take(new InGameAdvanced(InGameStates.Ffx(samples: 1)));
        IReadOnlyList<AdvancedFact> first = sampler.Facts;

        sampler.Take(new InGameAdvanced(InGameStates.Ffx(samples: 2)));
        sampler.Facts.Should().BeSameAs(first, "only counts, the frame time and the clock moved");

        sampler.Take(new InGameAdvanced(InGameStates.Ffx(samples: 3) with { FfxFovMaxBits = InGameStates.Bits(1.3f) }));
        sampler.Facts.Should().NotBeSameAs(first, "the player zoomed out: the range widened");
        Rows(Fact(sampler, "ffx.upscale.depth")).Should().Contain("cameraFovAngleVertical=0.9 – 1.3 rad (51.6° – 74.5°)");
    }

    [Fact]
    public void WhatTheGameTellsStreamlineEachFrameBecomesFiveFactsOfTheFirstViewport()
    {
        // 0.1.0-beta.43: slSetConstants. The jitter as its largest magnitude, the field of view as a range, each
        // Boolean as what it was seen to be over the session - and nothing of the camera's position, axes or
        // matrices, which the capture side never reads.
        var sampler = new InGameAdvancedSampler();
        sampler.Take(new InGameAdvanced(InGameStates.Streamline()));

        Rows(Fact(sampler, "sl.constants.jitter")).Should().Equal("jitterOffset.x=≤ 0.3125", "jitterOffset.y=≤ 0.4375");
        AdvancedFact motion = Fact(sampler, "sl.constants.motion_vectors");
        Rows(motion).Should().Equal(
            "mvecScale.x=0.5", "mvecScale.y=0.25", "cameraMotionIncluded=on", "motionVectors3D=off", "motionVectorsDilated=off", "motionVectorsJittered=on");
        motion.Value.Rows![2].Token.Should().Be("on", "the App translates the word");
        Rows(Fact(sampler, "sl.constants.depth")).Should().Equal("cameraNear=0.25", "cameraFar=4000", "depthInverted=on");
        Rows(Fact(sampler, "sl.constants.projection")).Should().Equal(
            "cameraFOV=0.8 – 1.2 rad (45.8° – 68.8°)", "cameraAspectRatio=1.75", "orthographicProjection=off");
        Fact(sampler, "sl.constants.resets").Value.Number.Should().Be(1);
        sampler.Facts.Should().NotContain(static f => string.Equals(f.Id, "sl.constants.camera", StringComparison.Ordinal));
        sampler.Facts.Should().Contain(static f => string.Equals(f.Id, "sl.dlss.presets", StringComparison.Ordinal), "the options of the same game stay beside them");
    }

    [Fact]
    public void AStreamlineConstantNobodySetIsAbsentAndAPlaneAtInfinityIsNotStreamlines()
    {
        // FLT_MAX is Streamline's "not set" and never reaches the Agent; if the pattern D119 gives FSR's planes turned up
        // here it would still be no value. A Boolean seen neither way has no row; a reset never seen is a count of 0.
        var sampler = new InGameAdvancedSampler();
        sampler.Take(new InGameAdvanced(InGameStates.Empty() with
        {
            ValidMask = FlAdvancedValid.SlConstants,
            SlConstantsCalls = 60,
            SlNearBits = FlAdvancedState.FloatInfinite,
            SlFarBits = BitConverter.SingleToUInt32Bits(500f),
            SlFlagsSeenFalse = 1u << 4,
        }));

        Rows(Fact(sampler, "sl.constants.depth")).Should().Equal("cameraFar=500");
        Rows(Fact(sampler, "sl.constants.projection")).Should().Equal("orthographicProjection=off");
        Fact(sampler, "sl.constants.resets").Value.Number.Should().Be(0, "the game called and never reset the history: that is a fact");
        sampler.Facts.Should().NotContain(static f => f.Id == "sl.constants.jitter" || f.Id == "sl.constants.motion_vectors");
    }

    [Fact]
    public void HowTheGameConfiguresFsrFrameGenerationIsOneFactOfSwitchesFlagsAndTheRectangle()
    {
        var sampler = new InGameAdvancedSampler();
        sampler.Take(new InGameAdvanced(InGameStates.FrameGeneration()));

        AdvancedFact config = Fact(sampler, "ffx.framegen.config");
        Rows(config).Should().Equal(
            "frameGenerationEnabled=on", "allowAsyncWorkloads=off", "onlyPresentGenerated=off", "flags=DRAW_DEBUG_PACING_LINES", "generationRect=1920 × 1080 at 0, 0");
        config.Value.Rows![0].Token.Should().Be("on");

        // Switched off in the menu part-way, a flag the vendor's header does not name, and a rectangle the capture side
        // could not state (four -1): both states are said, the bit is shown as it is, and there is no rectangle row.
        var other = new InGameAdvancedSampler();
        other.Take(new InGameAdvanced(InGameStates.WithRect(
            InGameStates.FrameGeneration() with { FfxFgEnabledSeen = 3, FfxFgFlagsSeen = (1u << 0) | (1u << 9), FfxFgAsyncSeen = 0 }, -1, -1, -1, -1)));
        Rows(Fact(other, "ffx.framegen.config")).Should().Equal(
            "frameGenerationEnabled=on_and_off", "onlyPresentGenerated=off", "flags=DRAW_DEBUG_TEAR_LINES, bit 9");

        // A rectangle with no area is one the game did not give. A configure that set no flag says so, as an upscale
        // does (0.1.0-beta.44): the row was missing, and "no row" read as "not read".
        var none = new InGameAdvancedSampler();
        none.Take(new InGameAdvanced(InGameStates.WithRect(InGameStates.FrameGeneration() with { FfxFgFlagsSeen = 0 }, 0, 0, 0, 0)));
        AdvancedFact noFlags = Fact(none, "ffx.framegen.config");
        Rows(noFlags).Should().Equal("frameGenerationEnabled=on", "allowAsyncWorkloads=off", "onlyPresentGenerated=off", "flags=none");
        noFlags.Value.Rows![3].Token.Should().Be("none", "the App translates the word");
    }

    [Fact]
    public void AFieldOfViewOverPiIsSaidAsTheGameGaveItAndNeverConverted()
    {
        // D128. Cyberpunk 2077 gives Streamline its field of view in degrees - 51.0055 to 90 on 2026-11-12 - where the
        // vendor's header says radians, and the row read "51.0055 - 90 rad (2922.4 deg - 5156.6 deg)". A value over pi
        // cannot be radians: it is said as given, a second row says what it is not, and nothing is converted.
        var sampler = new InGameAdvancedSampler();
        sampler.Take(new InGameAdvanced(InGameStates.Streamline() with { SlFovMinBits = InGameStates.Bits(51.0055f), SlFovMaxBits = InGameStates.Bits(90f) }));

        AdvancedFact projection = Fact(sampler, "sl.constants.projection");
        Rows(projection).Should().Equal("cameraFOV=51.0055 – 90", "cameraFOV unit=not_radians", "cameraAspectRatio=1.75", "orthographicProjection=off");
        projection.Value.Rows![0].Token.Should().BeNull("the number is the game's");
        projection.Value.Rows![1].Token.Should().Be("not_radians", "the App says it in three languages");

        // One value, and FSR's field the same way; a range that only ends over pi is not half radians.
        var one = new InGameAdvancedSampler();
        one.Take(new InGameAdvanced(InGameStates.Ffx() with { FfxFovMinBits = InGameStates.Bits(75f), FfxFovMaxBits = InGameStates.Bits(75f) }));
        Rows(Fact(one, "ffx.upscale.depth")).Should().ContainInOrder("cameraFovAngleVertical=75", "cameraFovAngleVertical unit=not_radians");
        var mixed = new InGameAdvancedSampler();
        mixed.Take(new InGameAdvanced(InGameStates.Streamline() with { SlFovMinBits = InGameStates.Bits(1.2f), SlFovMaxBits = InGameStates.Bits(3.5f) }));
        Rows(Fact(mixed, "sl.constants.projection")).Take(2).Should().Equal("cameraFOV=1.2 – 3.5", "cameraFOV unit=not_radians");

        // Pi itself is a radian: a half-turn, which no game renders, and still not this rule's to refuse.
        var edge = new InGameAdvancedSampler();
        edge.Take(new InGameAdvanced(InGameStates.Streamline() with { SlFovMinBits = InGameStates.Bits(3.0f), SlFovMaxBits = InGameStates.Bits(3.0f) }));
        Rows(Fact(edge, "sl.constants.projection")).Take(2).Should().Equal("cameraFOV=3 rad (171.9°)", "cameraAspectRatio=1.75");
    }

    [Fact]
    public void WhatStreamlineAnsweredAboutEachFeatureIsTwoFactsInTheVendorsOwnNames()
    {
        // 0.1.0-beta.44: slGetFeatureVersion and slGetFeatureRequirements. A slot per feature in the order the game
        // first asked, every name the vendor's - the feature's constant, the struct's member, the flag's enumerator.
        var sampler = new InGameAdvancedSampler();
        sampler.Take(new InGameAdvanced(InGameStates.Features()));

        Rows(Fact(sampler, "sl.feature.versions")).Should().Equal(
            "kFeatureDLSS · versionSL=2.14.1",
            "kFeatureDLSS · versionNGX=310.4.0",
            "kFeatureDLSS_G · versionSL=2.14.1",
            "kFeatureDLSS_G · versionNGX=310.4.1");
        Rows(Fact(sampler, "sl.feature.requirements")).Should().Equal(
            "kFeatureDLSS · flags=eD3D11Supported, eD3D12Supported, eVulkanSupported",
            "kFeatureDLSS · maxNumCPUThreads=2",
            "kFeatureDLSS · maxNumViewports=32",
            "kFeatureDLSS · numRequiredTags=3",
            "kFeatureDLSS · osVersionDetected=10.0.26100",
            "kFeatureDLSS · osVersionRequired=10.0.19041",
            "kFeatureDLSS · driverVersionDetected=591.44.0",
            "kFeatureDLSS · driverVersionRequired=512.15.0",
            "kFeatureDLSS_G · flags=eD3D12Supported, eVSyncOffRequired, eHardwareSchedulingRequired",
            "kFeatureDLSS_G · maxNumCPUThreads=1",
            "kFeatureDLSS_G · maxNumViewports=1",
            "kFeatureDLSS_G · numRequiredTags=4",
            "kFeatureDLSS_G · osVersionDetected=10.0.26100",
            "kFeatureDLSS_G · osVersionRequired=10.0.19041",
            "kFeatureDLSS_G · driverVersionDetected=591.44.0",
            "kFeatureDLSS_G · driverVersionRequired=551.23.0");
        sampler.Facts.Should().Contain(static f => string.Equals(f.Id, "sl.constants.jitter", StringComparison.Ordinal), "the constants of the same game stay beside them");
    }

    [Fact]
    public void AFeatureAnsweredInPartAnIdWithNoNameAndAVersionNobodyStatedAreSaidAsTheyAre()
    {
        // Slot 0: only the version, of a feature with no NGX model - no versionNGX row. Slot 1: only the requirements,
        // of an id this build has no name for, two flag bits the header does not name, counts of 0 that are counts, and
        // one version stated out of four. Slot 2: a feature that requires nothing says so. Slot 4 is past an empty slot:
        // slots fill from 0, so it is not a slot.
        FlAdvancedState s = InGameStates.WithFeature(InGameStates.Empty(), 0, 3, FlAdvancedState.FeatureVersionAnswered, 2, 14, 1);
        s = InGameStates.WithFeature(s, 1, 4242, FlAdvancedState.FeatureRequirementsAnswered, 0, 0, 0, 0, 0, 0, 0x120, 0, 1, 0, 0, 0, 0, 10, 0, 22000);
        s = InGameStates.WithFeature(s, 2, 2, FlAdvancedState.FeatureRequirementsAnswered);
        s = InGameStates.WithFeature(s, 4, 5, FlAdvancedState.FeatureVersionAnswered, 9, 9, 9);
        var sampler = new InGameAdvancedSampler();
        sampler.Take(new InGameAdvanced(s));

        Rows(Fact(sampler, "sl.feature.versions")).Should().Equal("kFeatureReflex · versionSL=2.14.1");
        AdvancedFact requirements = Fact(sampler, "sl.feature.requirements");
        Rows(requirements).Should().Equal(
            "feature 4242 · flags=bit 5, bit 8",
            "feature 4242 · maxNumCPUThreads=0",
            "feature 4242 · maxNumViewports=1",
            "feature 4242 · numRequiredTags=0",
            "feature 4242 · osVersionRequired=10.0.22000",
            "kFeatureNIS · flags=none",
            "kFeatureNIS · maxNumCPUThreads=0",
            "kFeatureNIS · maxNumViewports=0",
            "kFeatureNIS · numRequiredTags=0");
        requirements.Value.Rows![5].Token.Should().Be("none", "the App translates the word");

        // A publication with the group's bit and no slot in use makes neither fact.
        var none = new InGameAdvancedSampler();
        none.Take(new InGameAdvanced(InGameStates.Empty() with { ValidMask = FlAdvancedValid.SlFeatures }));
        none.Facts.Should().BeEmpty();
    }

    [Fact]
    public void AnAnswerAboutAFeatureThatArrivesLaterMakesTheFactsNewAndEachQuestionIsItsOwn()
    {
        var sampler = new InGameAdvancedSampler();
        sampler.FeatureCall(FlAdvancedState.FeatureVersionAnswered, hookedIn: false).Should().Be(AdvancedInGameCall.NotHooked);
        sampler.FeatureCall(FlAdvancedState.FeatureVersionAnswered, hookedIn: true).Should().Be(AdvancedInGameCall.NeverCalled, "hooked, and the game did not ask again");

        sampler.Take(new InGameAdvanced(InGameStates.Streamline()));
        IReadOnlyList<AdvancedFact> before = sampler.Facts;
        before.Should().NotContain(static f => f.Id.StartsWith("sl.feature.", StringComparison.Ordinal));
        sampler.FeatureCall(FlAdvancedState.FeatureRequirementsAnswered, hookedIn: true).Should().Be(AdvancedInGameCall.NeverCalled);

        // The version of one feature is answered: the list is new, that question is read, the other is not.
        FlAdvancedState versionOnly = InGameStates.WithFeature(InGameStates.Streamline(2), 0, 0, FlAdvancedState.FeatureVersionAnswered, 2, 14, 1, 310, 4, 0);
        sampler.Take(new InGameAdvanced(versionOnly));
        sampler.Facts.Should().NotBeSameAs(before);
        Rows(Fact(sampler, "sl.feature.versions")).Should().Equal("kFeatureDLSS · versionSL=2.14.1", "kFeatureDLSS · versionNGX=310.4.0");
        sampler.FeatureCall(FlAdvancedState.FeatureVersionAnswered, hookedIn: true).Should().Be(AdvancedInGameCall.Read);
        sampler.FeatureCall(FlAdvancedState.FeatureRequirementsAnswered, hookedIn: true).Should().Be(AdvancedInGameCall.NeverCalled);
        sampler.FeatureCall(FlAdvancedState.FeatureRequirementsAnswered, hookedIn: false).Should().Be(AdvancedInGameCall.NotHooked);

        // The same slots again: the same list. Both questions answered: a new one, and both read.
        IReadOnlyList<AdvancedFact> held = sampler.Facts;
        sampler.Take(new InGameAdvanced(InGameStates.WithFeature(InGameStates.Streamline(3), 0, 0, FlAdvancedState.FeatureVersionAnswered, 2, 14, 1, 310, 4, 0)));
        sampler.Facts.Should().BeSameAs(held, "nothing a fact is made of moved");
        sampler.Take(new InGameAdvanced(InGameStates.Features(4)));
        sampler.Facts.Should().NotBeSameAs(held);
        sampler.FeatureCall(FlAdvancedState.FeatureRequirementsAnswered, hookedIn: false).Should().Be(AdvancedInGameCall.Read, "a publication that carried it is the proof");
    }

    [Fact]
    public void ACountDoesNotRebuildTheFactsWhileTheSessionRunsAndIsExactOnceItConcludes()
    {
        // The resets told to Streamline are a count. While the session runs the list is rebuilt only when a value a
        // fact is made of moves - the session's context is rewritten on every new list - so the count is as of the
        // last rebuild; Conclude brings it to the last publication, once.
        var sampler = new InGameAdvancedSampler();
        sampler.Take(new InGameAdvanced(InGameStates.Streamline(1, resets: 1)));
        IReadOnlyList<AdvancedFact> first = sampler.Facts;

        sampler.Take(new InGameAdvanced(InGameStates.Streamline(2, resets: 5)));
        sampler.Facts.Should().BeSameAs(first, "a count alone rebuilds nothing");
        Fact(sampler, "sl.constants.resets").Value.Number.Should().Be(1);

        sampler.Conclude();
        sampler.Facts.Should().NotBeSameAs(first);
        Fact(sampler, "sl.constants.resets").Value.Number.Should().Be(5);

        IReadOnlyList<AdvancedFact> concluded = sampler.Facts;
        sampler.Conclude();
        sampler.Facts.Should().BeSameAs(concluded, "nothing moved since");

        var empty = new InGameAdvancedSampler();
        empty.Conclude();
        empty.Facts.Should().BeEmpty("a session that read nothing has nothing to bring up to date");
    }

    [Fact]
    public void ACallHookedOnlyForAdvancedMetricsIsReadOrHookedAndNeverMadeOrNotHooked()
    {
        var sampler = new InGameAdvancedSampler();
        sampler.Call(FlAdvancedValid.SlConstants, hookedIn: false).Should().Be(AdvancedInGameCall.NotHooked);
        sampler.Call(FlAdvancedValid.SlConstants, hookedIn: true).Should().Be(AdvancedInGameCall.NeverCalled);

        sampler.Take(new InGameAdvanced(InGameStates.Streamline()));
        sampler.Call(FlAdvancedValid.SlConstants, hookedIn: true).Should().Be(AdvancedInGameCall.Read);
        sampler.Call(FlAdvancedValid.SlConstants, hookedIn: false).Should().Be(AdvancedInGameCall.Read, "a publication that carried it is the proof, whatever the hook word says");
        sampler.Call(FlAdvancedValid.FfxFgConfig, hookedIn: true).Should().Be(AdvancedInGameCall.NeverCalled, "the other call is its own question");

        // Read once is read for the session, though a later publication - a new request's - does not carry it.
        sampler.Take(new InGameAdvanced(InGameStates.Ffx(2)));
        sampler.Call(FlAdvancedValid.SlConstants, hookedIn: true).Should().Be(AdvancedInGameCall.Read);
    }

    [Fact]
    public void EveryFactItMakesIsAnEntryTheCatalogSaysIsReadInsideAGame()
    {
        var sampler = new InGameAdvancedSampler();
        FlAdvancedState both = InGameStates.WithRect(InGameStates.Streamline(), 0, 0, 2560, 1440) with
        {
            FfxFgConfigures = 60,
            FfxFgEnabledSeen = 2,
            FfxSharpenSeen = 2,
            FfxFlagsSeen = 1u << 2,
            FfxFrameTimeCount = 60,
            FfxFrameTimeSumUs = 60 * 16_000,
            FfxMemoryTotalBytes = 256UL * 1024 * 1024,
            FfxMemoryQueries = 60,
            ValidMask = FlAdvancedValid.DlssOptions | FlAdvancedValid.FfxUpscale | FlAdvancedValid.FfxMemory | FlAdvancedValid.FfxFgConfig | FlAdvancedValid.SlConstants,
            DlssOutputWidth = 3840,
            DlssOutputHeight = 2160,
            DlssColorBuffersHdr = 2,
        };

        both = InGameStates.WithFeature(both, 0, 0, FlAdvancedState.FeatureVersionAnswered | FlAdvancedState.FeatureRequirementsAnswered, 2, 14, 1, 310, 4, 0, 7, 2, 32, 3);

        IReadOnlyList<AdvancedReading> readings = sampler.Take(new InGameAdvanced(both))!;

        sampler.Facts.Should().OnlyContain(static f => AdvancedMetricCatalog.Find(f.Id)!.Reach == AdvancedReach.Game);
        sampler.Facts.Select(static f => f.Id).Should().Contain(
            ["sl.constants.jitter", "sl.constants.resets", "ffx.framegen.config", "ffx.upscale.flags", "sl.dlss.presets", "sl.feature.versions", "sl.feature.requirements"]);
        AdvancedJson.Storable(sampler.Facts).Should().HaveCount(sampler.Facts.Count, "what the game said may be stored");
        readings.Select(static r => AdvancedMetricCatalog.Find(r.Series[AdvancedMetric.SeriesPrefix.Length..])!.Reach).Should().OnlyContain(static reach => reach == AdvancedReach.Game);
    }
}
