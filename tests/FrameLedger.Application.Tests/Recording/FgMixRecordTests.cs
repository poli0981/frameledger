// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Metrics;

namespace FrameLedger.Application.Tests.Recording;

/// <summary><c>sessions.fg_mix</c> back from its text, read by today's rule (beta.26).</summary>
public sealed class FgMixRecordTests
{
    /// <summary>The owner's Silent Hill 2 session (2026-10-19, 0.1.0-beta.25, the NVIDIA App's Dynamic multi frame generation), as stored.</summary>
    private const string _silentHill2 =
        "{\"Rows\":[" +
        "{\"Kind\":\"generated\",\"Factor\":2.0194174757281553,\"NativeFps\":111.82857142857142,\"DisplayedFps\":225.82857142857142,\"Seconds\":35,\"Share\":0.17703207945353897,\"Windows\":7}," +
        "{\"Kind\":\"generated\",\"Factor\":2.999266324284666,\"NativeFps\":95.75152407846059,\"DisplayedFps\":287.18432166745924,\"Seconds\":42.7042811,\"Share\":0.21600079099147035,\"Windows\":9}," +
        "{\"Kind\":\"generated\",\"Factor\":6.057471264367816,\"NativeFps\":23.2,\"DisplayedFps\":140.53333333333333,\"Seconds\":15,\"Share\":0.07587089119437383,\"Windows\":3}," +
        "{\"Kind\":\"generated\",\"Factor\":1336,\"NativeFps\":0.2,\"DisplayedFps\":267.2,\"Seconds\":10,\"Share\":0.05058059412958256,\"Windows\":2}," +
        "{\"Kind\":\"none\",\"Factor\":null,\"NativeFps\":80.08,\"DisplayedFps\":80.08,\"Seconds\":75,\"Share\":0.3793544559718692,\"Windows\":15}," +
        "{\"Kind\":\"unclassified\",\"Factor\":null,\"NativeFps\":null,\"DisplayedFps\":null,\"Seconds\":20,\"Share\":0.10116118825916512,\"Windows\":4}]}";

    [Fact]
    public void AStoredStateNoWindowOfThisBuildWouldFormIsTimeNoStateClaims()
    {
        FgMixRecord mix = FgMixRecord.Parse(_silentHill2)!;

        mix.Rows.Select(static r => r.Kind).Should().Equal("generated", "generated", "generated", "none", "unclassified");
        mix.Rows.Where(static r => string.Equals(r.Kind, "generated", StringComparison.Ordinal)).Select(static r => Math.Round(r.Factor!.Value, 1)).Should().Equal(2.0, 3.0, 6.1);
        FgMixRecordRow unclassified = mix.Rows[^1];
        unclassified.Seconds.Should().Be(30, "its own twenty seconds and the ten the ×1336.0 row held");
        unclassified.Windows.Should().Be(6);
        unclassified.Factor.Should().BeNull();
        unclassified.NativeFps.Should().BeNull("time no state claims has no rate to show");
        mix.Rows.Sum(static r => r.Share).Should().BeApproximately(1.0, 1e-9, "the shares still add up to the whole");
    }

    [Fact]
    public void AStoredMixOfReadableStatesIsReadAsStored()
    {
        string json = FgMixRecord.ToJson(new FgMix(
        [
            new FgMixRow(FgMixKind.Generated, 2.0, 60, 120, 60, 0.6, 12),
            new FgMixRow(FgMixKind.Generated, 8.0, 30, 240, 20, 0.2, 4),
            new FgMixRow(FgMixKind.None, null, 120, 120, 20, 0.2, 4),
        ]))!;

        FgMixRecord mix = FgMixRecord.Parse(json)!;

        mix.Rows.Select(static r => r.Kind).Should().Equal("generated", "generated", "none");
        mix.Rows[1].Factor.Should().Be(8.0, "at the ceiling a ratio is still frame generation's");
    }

    [Theory]
    [InlineData(8.01, 30.0)]
    [InlineData(4.0, 1.5)]
    [InlineData(null, 30.0)]
    public void AGeneratedRowAboveTheCeilingOrWithTooFewFramesIsFoldedEvenWithNoUnclassifiedRow(double? factor, double nativeFps)
    {
        string json = "{\"Rows\":[" +
            "{\"Kind\":\"generated\",\"Factor\":2.0,\"NativeFps\":60,\"DisplayedFps\":120,\"Seconds\":40,\"Share\":0.8,\"Windows\":8}," +
            $"{{\"Kind\":\"generated\",\"Factor\":{(factor is double f ? f.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null")},\"NativeFps\":{nativeFps.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"DisplayedFps\":240,\"Seconds\":10,\"Share\":0.2,\"Windows\":2}}]}}";

        FgMixRecord mix = FgMixRecord.Parse(json)!;

        mix.Rows.Select(static r => r.Kind).Should().Equal("generated", "unclassified");
        mix.Rows[1].Seconds.Should().Be(10);
        mix.Rows[1].Share.Should().Be(0.2);
        mix.Rows[1].Windows.Should().Be(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    public void NoTextOrNoJsonIsNoMix(string? json) => FgMixRecord.Parse(json).Should().BeNull();
}
