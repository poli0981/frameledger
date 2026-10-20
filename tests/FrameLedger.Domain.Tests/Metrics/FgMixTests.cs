// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.Domain.Metrics;
using static FrameLedger.Domain.Tests.Metrics.SampleFixtures;

namespace FrameLedger.Domain.Tests.Metrics;

/// <summary>
/// beta.24 (decision D62): every frame-generation state a session held, each with its share of the presenting time —
/// the steady state's windows, all of them. 240 presents per second in these fixtures, as in
/// <see cref="FgSteadyStateTests"/>, so five seconds is 1,200 presents.
/// </summary>
public sealed class FgMixTests
{
    private const int _presentsPerSecond = 240;

    private static List<FrameSample> Play(int seconds, int k) =>
        FgStream(appFrames: seconds * _presentsPerSecond / k, k: k);

    private static List<FrameSample> Concat(params List<FrameSample>[] runs)
    {
        List<FrameSample> all = runs[0];
        for (int i = 1; i < runs.Length; i++)
        {
            all = SampleFixtures.Concat(all, runs[i]);
        }

        return all;
    }

    private static void AddsUpToTheWhole(FgMix mix) =>
        mix.Rows.Sum(static r => r.Share).Should().BeApproximately(1.0, 1e-9, "every window lands in exactly one row");

    [Fact]
    public void MenusThenTwoThreeAndFourTimesAreFourRowsInOrder()
    {
        FgMix mix = FgMix.From(Concat(Play(20, 1), Play(30, 2), Play(40, 3), Play(50, 4)), Frequency, noneAllowed: true)!;

        mix.Rows.Select(static r => r.Kind).Should().Equal(FgMixKind.Generated, FgMixKind.Generated, FgMixKind.Generated, FgMixKind.None);
        mix.Rows[0].Factor.Should().BeApproximately(2.0, 0.02);
        mix.Rows[1].Factor.Should().BeApproximately(3.0, 0.02);
        mix.Rows[2].Factor.Should().BeApproximately(4.0, 0.02);
        mix.Rows[0].Share.Should().BeApproximately(30.0 / 140, 0.03);
        mix.Rows[1].Share.Should().BeApproximately(40.0 / 140, 0.03);
        mix.Rows[2].Share.Should().BeApproximately(50.0 / 140, 0.03);
        mix.Rows[3].Share.Should().BeApproximately(20.0 / 140, 0.03);
        mix.Rows[3].Factor.Should().BeNull("no frame generation has no factor to show");
        mix.Rows[3].DisplayedFps.Should().BeApproximately(240, 3);
        foreach (FgMixRow state in mix.Rows.Take(3))
        {
            (state.NativeFps!.Value * state.Factor!.Value).Should().BeApproximately(state.DisplayedFps!.Value, 1, "each state's rates are its own, counted independently");
        }

        AddsUpToTheWhole(mix);
    }

    [Fact]
    public void TheLongestStateIsTheSteadyState()
    {
        List<FrameSample> session = Concat(Play(20, 1), Play(30, 3), Play(60, 4));

        FgMix mix = FgMix.From(session, Frequency, noneAllowed: true)!;
        FgSteadyState steady = FgWindow.From(session, Frequency).Steady!;

        FgMixRow longest = mix.Rows.Where(static r => r.Kind == FgMixKind.Generated).MaxBy(static r => r.Seconds)!;
        longest.Factor.Should().Be(steady.Factor);
        longest.Share.Should().Be(steady.Share);
        longest.Windows.Should().Be(steady.Windows);
    }

    [Fact]
    public void FiveAndSixTimesStayApart()
    {
        FgMix mix = FgMix.From(Concat(Play(30, 5), Play(30, 6)), Frequency, noneAllowed: true)!;

        mix.Rows.Where(static r => r.Kind == FgMixKind.Generated).Select(static r => Math.Round(r.Factor!.Value)).Should().Equal(5, 6);
        AddsUpToTheWhole(mix);
    }

    [Fact]
    public void ABurstShorterThanTenSecondsIsNotAState()
    {
        FgMix mix = FgMix.From(Concat(Play(60, 2), Play(6, 4)), Frequency, noneAllowed: true)!;

        mix.Rows.Select(static r => r.Kind).Should().Equal(FgMixKind.Generated, FgMixKind.Unclassified);
        mix.Rows[0].Factor.Should().BeApproximately(2.0, 0.02);
        mix.Rows[1].Seconds.Should().BeApproximately(6, 2);
        mix.Rows[1].NativeFps.Should().BeNull("time no state claims has no rate to show");
        AddsUpToTheWhole(mix);
    }

    [Fact]
    public void WhereACountedNoneIsWithheldItsWindowsAreUnclassified()
    {
        List<FrameSample> session = Concat(Play(20, 1), Play(40, 4));

        FgMix.From(session, Frequency, noneAllowed: true)!.Rows.Select(static r => r.Kind).Should().Equal(FgMixKind.Generated, FgMixKind.None);
        FgMix withheld = FgMix.From(session, Frequency, noneAllowed: false)!;
        withheld.Rows.Select(static r => r.Kind).Should().Equal(FgMixKind.Generated, FgMixKind.Unclassified);
        withheld.Rows[1].Share.Should().BeApproximately(1.0 / 3, 0.03);
    }

    [Fact]
    public void NoTokensAndABandFactorAreUnclassified()
    {
        List<FrameSample> noTokens = FgStream(appFrames: 15 * _presentsPerSecond, k: 1, evalsPerFrame: 0);
        // Four presents for three application frames: 1.33, between none and generating.
        List<FrameSample> band = FgStream(appFrames: 15 * _presentsPerSecond / 4, k: 4, evalsPerFrame: 3);

        FgMix mix = FgMix.From(Concat(noTokens, band, Play(40, 2)), Frequency, noneAllowed: true)!;

        mix.Rows.Select(static r => r.Kind).Should().Equal(FgMixKind.Generated, FgMixKind.Unclassified);
        mix.Rows[1].Seconds.Should().BeApproximately(30, 2);
        AddsUpToTheWhole(mix);
    }

    [Fact]
    public void AFactorThatWandersStillAddsUpAndNamesNoFactorItNeverHad()
    {
        // Dynamic frame generation: the multiplier moves every two seconds.
        var runs = new List<List<FrameSample>>();
        for (int i = 0; i < 30; i++)
        {
            runs.Add(Play(2, 2 + (i % 5)));
        }

        FgMix? mix = FgMix.From(Concat([.. runs]), Frequency, noneAllowed: true);

        if (mix is not null)
        {
            AddsUpToTheWhole(mix);
            mix.Rows.Where(static r => r.Kind == FgMixKind.Generated).Should().OnlyContain(static r => r.Factor >= 2 && r.Factor <= 6);
        }
    }

    /// <summary>
    /// beta.26: Silent Hill 2 (2026-10-19) presented 1,336 times per token for ten seconds — its first seconds, compiling
    /// shaders outside the vendor's frame count — and the mix listed it as a ×1336.0 state, which the DLSS line then
    /// cited. A window needs application frames, not only presents, and a ratio frame generation can
    /// produce.
    /// </summary>
    [Fact]
    public void AWindowWithOneTokenOrARatioNoFrameGenerationProducesIsNotAState()
    {
        // One token per 1,200 presents: one per five-second window, for ten seconds.
        List<FrameSample> loading = FgStream(appFrames: 2, k: 1200);
        // Twelve tokens per window at ×100: frames enough to read, a ratio no frame generation produces.
        List<FrameSample> beyond = FgStream(appFrames: 15 * _presentsPerSecond / 100, k: 100);

        FgMix mix = FgMix.From(Concat(Play(30, 3), loading, beyond, Play(20, 6)), Frequency, noneAllowed: true)!;

        mix.Rows.Select(static r => r.Kind).Should().Equal(FgMixKind.Generated, FgMixKind.Generated, FgMixKind.Unclassified);
        mix.Rows.Where(static r => r.Kind == FgMixKind.Generated).Select(static r => Math.Round(r.Factor!.Value)).Should().Equal(3, 6);
        mix.Rows[2].Seconds.Should().BeApproximately(25, 2, "the ten seconds of one-token windows and the fifteen at ×100");
        mix.Rows.Should().OnlyContain(static r => r.Factor == null || r.Factor <= FgWindow.GenerationCeiling);
        AddsUpToTheWhole(mix);
    }

    /// <summary>
    /// beta.27: Silent Hill 2 (2026-10-20) under the NVIDIA App's Dynamic multi frame generation held ×2 and ×3, and the
    /// windows where the multiplier stepped read ×2.21–×2.70; three of them formed a ×2.52 "state" nobody held. A window
    /// between two whole multipliers changed state inside itself, and is not classified.
    /// </summary>
    [Fact]
    public void WindowsBetweenTwoMultipliersAreNotAState()
    {
        // Thirty seconds stepping between ×2 and ×3 every second: every five-second window reads 2.3–2.5.
        List<FrameSample> stepping = Play(1, 2);
        for (int s = 1; s < 30; s++)
        {
            stepping = SampleFixtures.Concat(stepping, Play(1, s % 2 == 0 ? 2 : 3));
        }

        FgMix mix = FgMix.From(Concat(Play(20, 2), stepping, Play(20, 3)), Frequency, noneAllowed: true)!;

        mix.Rows.Select(static r => r.Kind).Should().Equal(FgMixKind.Generated, FgMixKind.Generated, FgMixKind.Unclassified);
        mix.Rows.Where(static r => r.Kind == FgMixKind.Generated).Select(static r => Math.Round(r.Factor!.Value)).Should().Equal(2, 3);
        mix.Rows[2].Seconds.Should().BeApproximately(30, 2);
        mix.Rows.Should().OnlyContain(static r => r.Factor == null || FgWindow.IsNearWholeMultiplier(r.Factor.Value));
        AddsUpToTheWhole(mix);
    }

    [Theory]
    [InlineData(1.79, false)]
    [InlineData(1.8, true)]
    [InlineData(2.2, true)]
    [InlineData(2.21, false)]
    [InlineData(2.52, false)]
    [InlineData(2.7, true)]
    [InlineData(6.06, true)]
    [InlineData(8.0, true)]
    public void AWholeMultiplierIsWithinTenPercent(double factor, bool near) =>
        FgWindow.IsNearWholeMultiplier(factor).Should().Be(near);

    [Fact]
    public void ARatioAtTheCeilingIsStillAState()
    {
        FgMix mix = FgMix.From(Concat(Play(20, 2), Play(20, 8)), Frequency, noneAllowed: true)!;

        mix.Rows.Where(static r => r.Kind == FgMixKind.Generated).Select(static r => Math.Round(r.Factor!.Value)).Should().Equal(2, 8);
    }

    [Fact]
    public void ASessionWithNoStateHasNoMix()
    {
        FgMix.From(Play(60, 1), Frequency, noneAllowed: true).Should().BeNull("no frame-generation state, nothing to split");
        FgMix.From(FgStream(appFrames: 1_000, k: 2, counted: false), Frequency, noneAllowed: true).Should().BeNull("nothing counted application frames");
        FgMix.From(Play(60, 2).Take(1).ToList(), Frequency, noneAllowed: true).Should().BeNull("one sample spans no time");
    }

    [Fact]
    public void AWindowThatLastsNoTimeMakesNoRow()
    {
        List<FrameSample> session = Play(60, 2);
        ulong boundary = session[0].Qpc + (ulong)(60 * Frequency);
        // One sample opening a new window, and eight at one instant that read none: both last no time.
        session.Add(session[^1] with { Qpc = boundary, FgEvaluations = 1 });
        FgMix lonely = FgMix.From(session, Frequency, noneAllowed: true)!;
        lonely.Rows.Should().ContainSingle().Which.Share.Should().Be(1.0);

        List<FrameSample> instant = Play(60, 2);
        for (int i = 0; i < FgWindow.MinPerBucket; i++)
        {
            instant.Add(instant[^1] with { Qpc = boundary, FgEvaluations = 1 });
        }

        FgMix.From(instant, Frequency, noneAllowed: true)!.Rows.Should().ContainSingle();
    }

    [Fact]
    public void ItChecksItsArguments()
    {
        Action nullSamples = () => FgMix.From(null!, Frequency, noneAllowed: true);
        nullSamples.Should().Throw<ArgumentNullException>();
        Action noClock = () => FgMix.From(Play(10, 2), 0, noneAllowed: true);
        noClock.Should().Throw<ArgumentOutOfRangeException>();
    }
}
