using FluentAssertions;
using FrameLedger.Domain.Metrics;
using static FrameLedger.Domain.Tests.Metrics.SampleFixtures;

namespace FrameLedger.Domain.Tests.Metrics;

/// <summary>
/// <c>03_METRICS</c> §Frame Generation, the steady state (2026-09-17). The shapes below are the owner's nine hooked
/// sessions of 2026-09-16, rebuilt from their stored per-frame counts: every one opened with a splash, a menu or a
/// loading screen, the whole-session factor was refused as non-uniform (rightly — 2.22 on a session whose gameplay
/// read ×4.1), and the gameplay's own number went with it. 240 presents per second in these fixtures, so five seconds
/// is 1,200 presents.
/// </summary>
public sealed class FgSteadyStateTests
{
    private const int _presentsPerSecond = 240;

    /// <summary><paramref name="seconds"/> of presents at factor <paramref name="k"/> (1 = every present carries a token).</summary>
    private static List<FrameSample> Play(int seconds, int k, uint swapchain = 1) =>
        FgStream(appFrames: seconds * _presentsPerSecond / k, k: k, swapchain: swapchain);

    /// <summary><paramref name="seconds"/> of presents with the count claimed and no token at all: a splash, a video, a loading screen.</summary>
    private static List<FrameSample> NoTokens(int seconds) =>
        FgStream(appFrames: seconds * _presentsPerSecond, k: 1, evalsPerFrame: 0);

    [Fact]
    public void AMenuThenGameplayIsRefusedAsASessionAndPublishedAsItsSteadyState()
    {
        // Cronos, 2026-09-16: menus at x1.0, then x2.00 for three quarters of the session.
        List<FrameSample> session = Concat(Play(20, 1), Play(60, 2));

        FgWindow w = FgWindow.From(session, Frequency);

        w.Factor.Should().BeNull("1.0 and 2.0 average to a configuration that never existed");
        w.Refusal!.Kind.Should().Be(FgRefusalKind.NonUniform);
        w.Steady.Should().NotBeNull();
        w.Steady!.Factor.Should().BeApproximately(2.0, 0.01);
        w.Steady.Share.Should().BeApproximately(0.75, 0.03);
        w.Steady.Seconds.Should().BeApproximately(60, 5);
        w.Steady.NativeFps.Should().BeApproximately(120, 2, "tokens per second over the state's own windows");
        w.Steady.DisplayedFps.Should().BeApproximately(240, 3);
        (w.Steady.NativeFps * w.Steady.Factor).Should().BeApproximately(w.Steady.DisplayedFps, 1, "counted independently, and they agree");
    }

    [Fact]
    public void AnOpeningWithNoTokensAtAllDoesNotCostTheGameplayItsNumber()
    {
        // Hell is Us, 2026-09-16: seven windows with no token, then x3.00.
        FgWindow w = FgWindow.From(Concat(NoTokens(15), Play(45, 3)), Frequency);

        w.Refusal!.Kind.Should().Be(FgRefusalKind.NonUniform);
        w.Steady!.Factor.Should().BeApproximately(3.0, 0.02);
        w.Steady.Share.Should().BeGreaterThan(0.7);
    }

    [Fact]
    public void TwoMultipliersNeverPoolAndTheLongerOneSpeaks()
    {
        // x3 and x4 are 25 % apart; pooled they would read 3.4, which no title offers.
        FgWindow w = FgWindow.From(Concat(Play(20, 3), Play(60, 4)), Frequency);

        w.Refusal!.Kind.Should().Be(FgRefusalKind.NonUniform);
        w.Steady!.Factor.Should().BeApproximately(4.0, 0.02);
        w.Steady.Share.Should().BeApproximately(0.75, 0.03);
    }

    [Fact]
    public void ABurstShorterThanTenSecondsOrUnderATenthOfTheSessionIsNotAState()
    {
        FgWindow.From(Concat(Play(60, 1), Play(6, 4)), Frequency).Steady.Should().BeNull("six seconds is a transition, not a configuration");
        FgWindow.From(Concat(Play(300, 1), Play(20, 4)), Frequency).Steady.Should().BeNull("twenty seconds of three hundred and twenty does not speak for the session");
    }

    [Fact]
    public void AUniformSessionPublishesItsOwnFactorAndCarriesNoSteadyState()
    {
        FgWindow w = FgWindow.From(Play(60, 2), Frequency);

        w.Refusal.Should().BeNull();
        w.Factor.Should().BeApproximately(2.0, 0.01);
        w.Steady.Should().BeNull("the steady state is what a refused session falls back to, never a second number beside a published one");
    }

    [Fact]
    public void ASessionThatNeverGeneratedHasNoSteadyState()
    {
        FgWindow w = FgWindow.From(Concat(NoTokens(15), Play(45, 1)), Frequency);

        w.Refusal!.Kind.Should().Be(FgRefusalKind.NonUniform);
        w.Steady.Should().BeNull("only an ACTIVE state is published this way; a none beside a gap keeps its refusal");
    }

    [Fact]
    public void ARecreatedSwapchainIsOneStreamAtATimeAndNoLongerRefuses()
    {
        // Onimusha, 2026-09-16: chain 1 for the menus, chain 2 from the first gameplay frame, nothing interleaved.
        FgWindow w = FgWindow.From(Concat(Play(20, 1, swapchain: 1), Play(60, 4, swapchain: 2)), Frequency);

        w.Streams.Should().Be(2);
        w.StreamsInterleaved.Should().BeFalse();
        w.Refusal!.Kind.Should().Be(FgRefusalKind.NonUniform, "the honest refusal for this session is that its state changed, not that it had two streams");
        w.Steady!.Factor.Should().BeApproximately(4.0, 0.02);

        FgWindow uniform = FgWindow.From(Concat(Play(30, 2, swapchain: 1), Play(30, 2, swapchain: 2)), Frequency);
        uniform.Refusal.Should().BeNull("two chains one after another at one factor is a uniform session");
        uniform.Factor.Should().BeApproximately(2.0, 0.01);
    }

    [Fact]
    public void CalledDirectlyItChecksItsArgumentsAndFindsNoStateWhereNothingGenerated()
    {
        List<FrameSample> none = Play(30, 1);

        FgSteadyState.From(none, 0, Frequency).Should().BeNull("no window reaches the active threshold");
        Action nullSamples = () => FgSteadyState.From(null!, 0, Frequency);
        nullSamples.Should().Throw<ArgumentNullException>();
        Action pastTheEnd = () => FgSteadyState.From(none, none.Count, Frequency);
        pastTheEnd.Should().Throw<ArgumentOutOfRangeException>();
        Action negative = () => FgSteadyState.From(none, -1, Frequency);
        negative.Should().Throw<ArgumentOutOfRangeException>();
        Action noClock = () => FgSteadyState.From(none, 0, 0);
        noClock.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ARefusalThatIsNotAboutUniformityGetsNoSteadyState()
    {
        List<FrameSample> interleaved = Concat(Play(20, 1), Play(60, 4));
        interleaved.Insert(9_000, interleaved[9_000] with { SwapchainId = 2, Qpc = interleaved[9_000].Qpc - 1 });

        FgWindow w = FgWindow.From(interleaved, Frequency);

        w.Refusal!.Kind.Should().Be(FgRefusalKind.MultipleStreams);
        w.Steady.Should().BeNull("a record set that cannot be attributed is not made trustworthy by slicing it");
    }
}
