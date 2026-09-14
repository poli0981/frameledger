using FluentAssertions;
using FrameLedger.App.Charts;

namespace FrameLedger.App.Tests;

/// <summary>FR-5.3: a 500k-point series draws as ≤ 2 × buckets points, keeps every bucket's min and max in order, and a short series is untouched.</summary>
public sealed class DecimatorTests
{
    [Fact]
    public void HalfAMillionPointsKeepTheirExtremesInABoundedOutput()
    {
        const int n = 500_000;
        var xs = new double[n];
        var ys = new double[n];
        for (int i = 0; i < n; i++)
        {
            xs[i] = i / 100.0;
            ys[i] = 16.6 + ((i * 7919L) % 1000) / 1000.0;   // deterministic jitter
        }

        ys[123_456] = 250;   // one spike
        ys[400_001] = 0.5;   // one dip

        (double[] dx, double[] dy) = Decimator.MinMax(xs, ys);

        dx.Length.Should().BeLessThanOrEqualTo(Decimator.DefaultBuckets * 2);
        dy.Should().Contain(250, "the spike survives any zoom");
        dy.Should().Contain(0.5, "so does the dip");
        dy.Max().Should().Be(ys.Max());
        dy.Min().Should().Be(ys.Min());
        dx.Should().BeInAscendingOrder("min and max are emitted in the order they occur");
    }

    [Fact]
    public void AShortSeriesIsReturnedAsIsAndABucketOfOneValueYieldsOnePoint()
    {
        double[] xs = [0, 1, 2, 3];
        double[] ys = [5, 6, 7, 8];
        (double[] dx, double[] dy) = Decimator.MinMax(xs, ys, buckets: 10);
        dx.Should().Equal(xs);
        dy.Should().Equal(ys);

        double[] flat = [.. Enumerable.Repeat(3.0, 100)];
        double[] fx = [.. Enumerable.Range(0, 100).Select(static i => (double)i)];
        (double[] gx, double[] gy) = Decimator.MinMax(fx, flat, buckets: 4);
        gx.Length.Should().Be(4, "a bucket whose min is its max contributes one point");
        gy.Should().OnlyContain(static v => v == 3.0);
    }

    [Fact]
    public void MismatchedLengthsAreRefused()
    {
        Action act = () => Decimator.MinMax([1, 2], [1], 10);
        act.Should().Throw<ArgumentException>();
    }
}
