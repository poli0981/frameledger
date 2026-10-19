// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.Domain.Tests.Metrics;

/// <summary>One quality rule for the row, its segments and the live card (beta.25, D65): the told modal, and FSR's withheld at a dynamic resolution.</summary>
public sealed class QualityByteTests
{
    private const MeasuredFields _claims = MeasuredFields.Upscaler | MeasuredFields.UpscalerParams;

    private static FrameSample Sample(byte quality, ushort renderW, ushort renderH, UpscalerKind upscaler = UpscalerKind.Fsr3,
        MeasuredFields measured = _claims) => new()
        {
            SwapchainId = 1,
            RenderW = renderW,
            RenderH = renderH,
            OutputW = 1920,
            OutputH = 1080,
            Upscaler = upscaler,
            UpscalerQuality = quality,
            UpscalerSharpness = 0xFF,
            Measured = measured,
        };

    private static List<FrameSample> Repeat(int count, FrameSample s) => [.. Enumerable.Repeat(s, count)];

    [Fact]
    public void NothingToldIsNoByte()
    {
        QualityByte.Of([]).Should().BeNull();
        QualityByte.Of(Repeat(40, Sample(FrameSample.QualityNotTold, 1280, 720))).Should().BeNull("0xFF is not a mode");
    }

    [Fact]
    public void ADlssByteIsTheModalOfTheToldAndATieGoesToTheLower()
    {
        List<FrameSample> dlss = [.. Repeat(10, Sample(3, 1485, 835, UpscalerKind.Dlss)), .. Repeat(20, Sample(FrameSample.QualityNotTold, 1485, 835, UpscalerKind.Dlss))];
        QualityByte.Of(dlss).Should().Be(3, "the frames that told are the only ones that count");

        List<FrameSample> tie = [.. Repeat(5, Sample(2, 1485, 835, UpscalerKind.Dlss)), .. Repeat(5, Sample(1, 1280, 720, UpscalerKind.Dlss))];
        QualityByte.Of(tie).Should().Be(1, "a tie goes to the lower byte, as the modal always has");
    }

    [Fact]
    public void FsrAtTheSizeItsQueryGaveIsNamedEvenBesideNotToldRecordsAtThatSize()
    {
        // 0xFF at the named size: a present that drained a frame-generation PREPARE alone, or a frame before the title
        // asked. Neither is a size no answer gave.
        List<FrameSample> fixedQuality = [.. Repeat(30, Sample(0x11, 1280, 720)), .. Repeat(10, Sample(FrameSample.QualityNotTold, 1280, 720))];

        QualityByte.Of(fixedQuality).Should().Be(0x11);
    }

    [Fact]
    public void FsrAtADynamicResolutionIsWithheld()
    {
        List<FrameSample> dynamic = [.. Repeat(30, Sample(0x11, 1280, 720)), Sample(FrameSample.QualityNotTold, 1180, 664)];

        QualityByte.Of(dynamic).Should().BeNull("one FSR frame at a size no answer gave is a resolution the mode does not name");
    }

    [Fact]
    public void AModeChangeMidRunKeepsTheLongerModeWhileEachSizeIsNamed()
    {
        List<FrameSample> change = [
            .. Repeat(10, Sample(0x11, 1280, 720)),
            .. Repeat(25, Sample(0x13, 960, 540, UpscalerKind.FsrUnversioned)),
            .. Repeat(5, Sample(FrameSample.QualityNotTold, 960, 540, UpscalerKind.FsrUnversioned)),
        ];

        QualityByte.Of(change).Should().Be(0x13, "Quality then Performance, both asked: every size is one a named record has");
    }

    [Fact]
    public void OnlyFsrRecordsCanWithholdFsrsMode()
    {
        List<FrameSample> others = [
            .. Repeat(30, Sample(0x14, 640, 360, UpscalerKind.Fsr4)),
            .. Repeat(5, Sample(FrameSample.QualityNotTold, 1485, 835, UpscalerKind.Dlss)),
            .. Repeat(5, Sample(FrameSample.QualityNotTold, 999, 555, UpscalerKind.Unknown)),
            .. Repeat(5, Sample(FrameSample.QualityNotTold, 777, 444, measured: MeasuredFields.UpscalerParams)),
        ];

        QualityByte.Of(others).Should().Be(0x14, "another upscaler's sizes are its own, and a record that does not claim the upscaler names none");

        List<FrameSample> fsr2 = [.. Repeat(30, Sample(0x10, 1920, 1080)), Sample(FrameSample.QualityNotTold, 1600, 900, UpscalerKind.Fsr2)];
        QualityByte.Of(fsr2).Should().BeNull("an FSR record of any version at a size no answer gave");
    }

    [Theory]
    [InlineData(0x0F, false)]
    [InlineData(0x10, true)]
    [InlineData(0x14, true)]
    [InlineData(0x15, false)]
    [InlineData(0xFF, false)]
    public void FsrsBytesAreTheFiveFromNativeAaToUltraPerformance(byte value, bool fsr) =>
        QualityByte.IsFsr(value).Should().Be(fsr);

    [Fact]
    public void ANullListIsRefused() =>
        FluentActions.Invoking(static () => QualityByte.Of(null!)).Should().Throw<ArgumentNullException>();
}
