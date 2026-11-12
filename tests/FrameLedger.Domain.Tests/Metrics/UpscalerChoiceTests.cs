// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.Domain.Tests.Metrics;

/// <summary>
/// One technology's name, quality and size (owner decision D120): which upscaler a stream names, and which samples
/// its quality and render size may be read from.
/// </summary>
public sealed class UpscalerChoiceTests
{
    private static FrameSample Sample(UpscalerKind upscaler, MeasuredFields measured = MeasuredFields.Upscaler | MeasuredFields.UpscalerParams) => new()
    {
        SwapchainId = 1,
        Upscaler = upscaler,
        UpscalerQuality = FrameSample.QualityNotTold,
        Measured = measured,
    };

    private static List<FrameSample> Of(params (UpscalerKind Kind, int Count)[] runs) =>
        [.. runs.SelectMany(static r => Enumerable.Repeat(r.Kind, r.Count)).Select(static k => Sample(k))];

    [Fact]
    public void AStreamThatNamesNothingChoosesNothingAndKeepsEverySample()
    {
        List<FrameSample> stream = Of((UpscalerKind.Unknown, 40), (UpscalerKind.NotReported, 10), (UpscalerKind.RetiredRayReconstruction, 5));

        UpscalerChoice choice = UpscalerChoice.From(stream);

        choice.Kind.Should().BeNull("unknown, not reported and the retired value name no technology");
        choice.Named.Should().Be(0);
        choice.Samples.Should().BeSameAs(stream);
        UpscalerChoice.From([]).Kind.Should().BeNull();
        FluentActions.Invoking(static () => UpscalerChoice.From(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OneTechnologyNamedOnAFewPresentsIsTheChoiceAndEverySampleStillCounts()
    {
        // Under frame generation a name sits on one present in N: the others say "unknown" and still carry the
        // render size of the frame they belong to.
        List<FrameSample> stream = Of((UpscalerKind.Unknown, 3), (UpscalerKind.FsrUnversioned, 1), (UpscalerKind.Unknown, 3), (UpscalerKind.FsrUnversioned, 1));

        UpscalerChoice choice = UpscalerChoice.From(stream);

        choice.Kind.Should().Be(UpscalerKind.FsrUnversioned);
        choice.Named.Should().Be(1);
        choice.Samples.Should().BeSameAs(stream, "with one technology named nothing is left out, exactly as before");
    }

    [Fact]
    public void WithTwoTechnologiesNamedTheOneOnTheMostSamplesIsChosenAndOnlyItsSamplesAreRead()
    {
        // The session this exists for: DLSS seen first, briefly, then FSR for the rest. First-named said "DLSS".
        List<FrameSample> stream = Of((UpscalerKind.Dlss, 20), (UpscalerKind.Unknown, 15), (UpscalerKind.FsrUnversioned, 60));

        UpscalerChoice choice = UpscalerChoice.From(stream);

        choice.Kind.Should().Be(UpscalerKind.FsrUnversioned);
        choice.Named.Should().Be(2, "which is a setting changed mid-session");
        choice.Samples.Should().HaveCount(60).And.OnlyContain(static s => s.Upscaler == UpscalerKind.FsrUnversioned,
            "nothing of the other technology, and nothing unnamed, is carried under the chosen name");
    }

    [Fact]
    public void ATieGoesToTheLowerValueAndASampleThatDoesNotClaimTheFieldNamesNothing()
    {
        UpscalerChoice.From(Of((UpscalerKind.Fsr3, 10), (UpscalerKind.Dlss, 10))).Kind.Should().Be(UpscalerKind.Dlss, "as QualityByte.Of breaks its own ties");

        // A value set while its bit is clear is the writer's defect: it is not a name.
        List<FrameSample> unclaimed = [.. Enumerable.Repeat(Sample(UpscalerKind.Dlss, MeasuredFields.None), 50), Sample(UpscalerKind.Fsr3)];
        UpscalerChoice choice = UpscalerChoice.From(unclaimed);
        choice.Kind.Should().Be(UpscalerKind.Fsr3);
        choice.Named.Should().Be(1);
    }

    [Theory]
    [InlineData(UpscalerKind.Dlss, true)]
    [InlineData(UpscalerKind.Nis, true)]
    [InlineData(UpscalerKind.None, true)]
    [InlineData(UpscalerKind.FsrUnversioned, true)]
    [InlineData(UpscalerKind.Unknown, false)]
    [InlineData(UpscalerKind.NotReported, false)]
    [InlineData(UpscalerKind.RetiredRayReconstruction, false)]
    public void ATechnologyIsNamedByEveryValueButTheThreeThatSayNothing(UpscalerKind kind, bool named) =>
        UpscalerChoice.IsNamed(kind).Should().Be(named);

    [Theory]
    [InlineData(UpscalerKind.Fsr2, true)]
    [InlineData(UpscalerKind.Fsr3, true)]
    [InlineData(UpscalerKind.Fsr4, true)]
    [InlineData(UpscalerKind.FsrUnversioned, true)]
    [InlineData(UpscalerKind.Dlss, false)]
    [InlineData(UpscalerKind.XeSS, false)]
    public void FsrIsAFamilyOfFour(UpscalerKind kind, bool fsr) => UpscalerChoice.IsFsr(kind).Should().Be(fsr);

    [Fact]
    public void AQualityByteOfTheOtherFamilyIsWithheldRatherThanShownUnderTheName()
    {
        UpscalerChoice dlss = UpscalerChoice.From(Of((UpscalerKind.Dlss, 5)));
        UpscalerChoice fsr = UpscalerChoice.From(Of((UpscalerKind.Fsr3, 5)));
        UpscalerChoice none = UpscalerChoice.From(Of((UpscalerKind.Unknown, 5)));

        dlss.QualityFor(0x12).Should().BeNull("0x12 is FSR's Balanced; under DLSS it is not a mode anyone measured");
        dlss.QualityFor(3).Should().Be(3);
        fsr.QualityFor(0x12).Should().Be(0x12);
        fsr.QualityFor(3).Should().BeNull("a DLSS mode under an FSR name");
        fsr.QualityFor(null).Should().BeNull();
        none.QualityFor(0x12).Should().Be(0x12, "with no technology named there is no name to contradict");
    }
}
