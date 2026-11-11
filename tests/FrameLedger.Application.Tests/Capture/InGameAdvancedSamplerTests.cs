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
        InGameAdvancedSampler.Tokens.Should().Contain(["on", "off", "on_and_off", "none", "draw_debug_view", "non_linear_color_srgb", "non_linear_color_pq"],
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
    public void EveryFactItMakesIsAnEntryTheCatalogSaysIsReadInsideAGame()
    {
        var sampler = new InGameAdvancedSampler();
        FlAdvancedState both = InGameStates.Ffx() with
        {
            ValidMask = FlAdvancedValid.DlssOptions | FlAdvancedValid.FfxUpscale | FlAdvancedValid.FfxMemory,
            DlssOutputWidth = 3840,
            DlssOutputHeight = 2160,
            DlssColorBuffersHdr = 2,
        };

        IReadOnlyList<AdvancedReading> readings = sampler.Take(new InGameAdvanced(both))!;

        sampler.Facts.Should().OnlyContain(static f => AdvancedMetricCatalog.Find(f.Id)!.Reach == AdvancedReach.Game);
        AdvancedJson.Storable(sampler.Facts).Should().HaveCount(sampler.Facts.Count, "what the game said may be stored");
        readings.Select(static r => AdvancedMetricCatalog.Find(r.Series[AdvancedMetric.SeriesPrefix.Length..])!.Reach).Should().OnlyContain(static reach => reach == AdvancedReach.Game);
    }
}
