// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Infrastructure.Blobs;

namespace FrameLedger.Infrastructure.Tests.Blobs;

/// <summary>
/// <c>14_TESTING</c>: "blob codecs round-trip for every series incl. two-pair render_res and three-bit
/// rt_flags, NaN forbidden".
/// </summary>
public sealed class SeriesCodecRoundTripTests
{
    [Fact]
    public void Float32RoundTripsAndCompresses()
    {
        float[] frametimes = [.. Enumerable.Range(0, 360_000).Select(i => 10f + (i % 7) * 0.5f)];

        byte[] blob = SeriesCodec.EncodeFloat32(frametimes);

        SeriesCodec.DecodeFloat32(blob).Should().Equal(frametimes);
        blob.Length.Should().BeLessThan(frametimes.Length * sizeof(float) / 2, "06_DATA_MODEL budgets an hour at 100 fps at <= 0.7 MB stored");
    }

    [Fact]
    public void NaNIsRefusedAtEncodeTime()
    {
        Action encode = () => SeriesCodec.EncodeFloat32([1f, float.NaN, 3f]);

        encode.Should().Throw<ArgumentException>().WithMessage("*index 1*");
    }

    [Fact]
    public void UInt16UInt32AndBytesRoundTrip()
    {
        ushort[] pso = [0, 1, 0, 40, ushort.MaxValue];
        uint[] rays = [0, 8_294_400, uint.MaxValue];
        byte[] rtFlags = [0b000, 0b001, 0b010, 0b100, 0b111];

        SeriesCodec.DecodeUInt16(SeriesCodec.EncodeUInt16(pso)).Should().Equal(pso);
        SeriesCodec.DecodeUInt32(SeriesCodec.EncodeUInt32(rays)).Should().Equal(rays);
        SeriesCodec.DecodeBytes(SeriesCodec.EncodeBytes(rtFlags)).Should().Equal(rtFlags, "all three bits preserved");
    }

    [Fact]
    public void EmptySeriesRoundTripToEmpty()
    {
        SeriesCodec.DecodeFloat32(SeriesCodec.EncodeFloat32([])).Should().BeEmpty();
        SeriesCodec.DecodeUInt32(SeriesCodec.EncodeUInt32([])).Should().BeEmpty();
    }

    [Fact]
    public void ATruncatedBlobIsRefusedRatherThanReadShort()
    {
        byte[] blob = SeriesCodec.EncodeUInt32([1, 2, 3]);
        // Re-deflate a 3-byte payload so the element boundary check is what fires, not the inflater.
        byte[] odd = SeriesCodec.EncodeBytes([1, 2, 3]);

        Action decode = () => SeriesCodec.DecodeUInt32(odd);

        decode.Should().Throw<InvalidDataException>();
        SeriesCodec.DecodeUInt32(blob).Should().Equal(1u, 2u, 3u);
    }

    [Fact]
    public void RenderResStoresTwoPairsPerFrame()
    {
        RenderRes[] frames = [new(1485, 835, 2560, 1440), new(1485, 835, 2560, 1440), new(1707, 960, 2560, 1440)];

        RenderRes[] back = RenderResCodec.Decode(RenderResCodec.Encode(frames));

        back.Should().Equal(frames);
        RenderResCodec.IsConstant(frames).Should().BeFalse();
        RenderResCodec.IsConstant(frames[..2]).Should().BeTrue("one tuple throughout, so the column may stay NULL");
        RenderResCodec.IsConstant([]).Should().BeTrue();
        SeriesCodec.DecodeUInt16(RenderResCodec.Encode(frames)).Should().HaveCount(12, "four uint16 per frame");
    }

    [Fact]
    public void ARenderResBlobWithAStrayPairIsRefused()
    {
        byte[] blob = SeriesCodec.EncodeUInt16([1485, 835, 2560]);

        Action decode = () => RenderResCodec.Decode(blob);

        decode.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void TheCodecTagIsTheOneTheSchemaStores() => SeriesCodec.Tag.Should().Be("deflate-le-v1");

    [Fact]
    public void ASparseSeriesKeepsItsGapsAsNaNAndItsNegativeValues()
    {
        // Owner decision D102: an advanced series has ticks with no reading, and may be negative, so -1 cannot mark a gap.
        float[] rail = [-11.97f, float.NaN, float.NaN, -12.03f, 0f];

        float[] back = SeriesCodec.DecodeFloat32(SeriesCodec.EncodeSparseFloat32(rail));

        back.Should().HaveCount(5);
        back[0].Should().Be(-11.97f);
        float.IsNaN(back[1]).Should().BeTrue();
        float.IsNaN(back[2]).Should().BeTrue();
        back[3].Should().Be(-12.03f);
        back[4].Should().Be(0f, "a zero is a reading");
        new DeflateSeriesCodec().EncodeSparseFloat32(rail).Should().Equal(SeriesCodec.EncodeSparseFloat32(rail));
    }

    [Fact]
    public void ASparseSeriesStillRefusesAnInfinityAndTheOrdinaryEncoderStillRefusesNaN()
    {
        Action infinite = () => SeriesCodec.EncodeSparseFloat32([1f, float.PositiveInfinity]);
        Action nan = () => SeriesCodec.EncodeFloat32([1f, float.NaN]);

        infinite.Should().Throw<ArgumentException>().WithMessage("*infinity at index 1*");
        nan.Should().Throw<ArgumentException>("the rule for frame times is not relaxed by the second encoder");
        SeriesCodec.DecodeFloat32(SeriesCodec.EncodeSparseFloat32([])).Should().BeEmpty();
        FluentActions.Invoking(static () => SeriesCodec.EncodeSparseFloat32(null!)).Should().Throw<ArgumentNullException>();
    }
}
