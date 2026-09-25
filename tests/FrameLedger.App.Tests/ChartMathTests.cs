using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.Application.Persistence;
using FrameLedger.Infrastructure.Blobs;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.8's honest charts, as the pure functions under them: the histogram's range and bar positions, where the percentile
/// curve may speak, what the frametime chart's note says, and the sensors chart's colours told apart in both themes.
/// </summary>
[Collection(StringsCultureCollection.Name)]
public sealed class ChartMathTests
{
    [Fact]
    public void TheHistogramIsBinnedBetweenTheHalfPercentilesAndDrawnAtBinCentres()
    {
        // 996 frames between 15 and 18 ms, and four that are not frames anyone saw: two presents 0.3 ms apart, two 200 ms hitches.
        List<double> frametimes = [.. Enumerable.Range(0, 996).Select(static i => 15 + ((i % 100) * 0.03)), 0.3, 0.3, 200, 200];

        FpsHistogram h = FpsHistogram.Of(frametimes, bins: 60);

        h.Centers.Should().HaveCount(60);
        h.Centers.Min().Should().BeGreaterThan(50, "a 5 FPS hitch does not stretch the axis");
        h.Centers.Max().Should().BeLessThan(70, "nor a 3,333 FPS non-frame");
        (h.Centers[1] - h.Centers[0]).Should().BeApproximately(h.BinWidth, 1e-9);
        h.Outside.Should().BeGreaterThanOrEqualTo(4);
        (h.Counts.Sum() + h.Outside).Should().Be(1_000, "every frame is in a bar or counted as outside");
    }

    [Fact]
    public void OneRateIsOneBarAndNothingIsNoBar()
    {
        FpsHistogram one = FpsHistogram.Of([.. Enumerable.Repeat(16.0, 50)]);
        one.Centers.Should().Equal(62.5);
        one.Counts.Should().Equal(50);

        FpsHistogram.Of([]).Centers.Should().BeEmpty();
    }

    [Theory]
    [InlineData(1_000, 99, true)]
    [InlineData(999, 99, false)]
    [InlineData(10_000, 99.9, true)]
    [InlineData(9_999, 99.9, false)]
    [InlineData(1_000_000, 100, false)]
    [InlineData(1_000_000, 0, false)]
    public void APercentileIsDrawnWhereTenFramesLieBeyondIt(int frames, double percent, bool drawn) =>
        PercentileCurve.Supported(frames, percent).Should().Be(drawn, "FR-4.8's guards are this rule: 1,000 frames for the 1% low, 10,000 for the 0.1%");

    [Fact]
    public void TheCurveStopsWhereTheSessionHasNoFramesToSayMore()
    {
        (double[] small, _) = PercentileCurve.Of([.. Enumerable.Range(0, 100).Select(static i => 16.0 + (i * 0.01))]);
        small.First().Should().Be(10);
        small.Last().Should().Be(90);
        PercentileCurve.Lows([.. Enumerable.Repeat(16.0, 100)]).Should().BeEmpty();

        (double[] mid, _) = PercentileCurve.Of([.. Enumerable.Repeat(16.0, 1_000)]);
        mid.Should().StartWith(1).And.EndWith(99).And.NotContain(0).And.NotContain(100);
        PercentileCurve.Lows([.. Enumerable.Repeat(16.0, 1_000)]).Should().ContainSingle().Which.X.Should().Be(99);

        (double[] large, _) = PercentileCurve.Of([.. Enumerable.Repeat(16.0, 10_000)]);
        large.Should().Contain(0.1).And.Contain(99.9).And.BeInAscendingOrder();
        PercentileCurve.Lows([.. Enumerable.Repeat(16.0, 10_000)]).Should().HaveCount(2);
    }

    [Fact]
    public void TheFrametimeNoteSaysWhatTheLinesAreNot() => InEnglish(() =>
    {
        SessionSeries plain = SessionSeriesLoader.Decode(Blob([0, 10, 10], [4, 0, 0]), [], []);
        SessionSeries generating = SessionSeriesLoader.Decode(Blob([0, 1, 9, 1, 9], [4, 1, 0, 1, 0]), [], []);
        SessionSeries allGenerated = SessionSeriesLoader.Decode(Blob([0, 10, 10], [5, 1, 1]), [], []);
        SessionSeries gapped = SessionSeriesLoader.Decode(Blob([0, 10, 0, 10], [4, 0, 4, 0]) with { FirstPresentMs = 0 }, [], []);

        FrametimeChart.NoteFor(plain, displayed: true, sensors: true).Should().BeEmpty();
        FrametimeChart.NoteFor(generating, displayed: false, sensors: false).Should().BeEmpty();
        FrametimeChart.NoteFor(generating, displayed: true, sensors: false).Should().Be(Strings.Chart_Note_PresentsTiming);
        FrametimeChart.NoteFor(allGenerated, displayed: false, sensors: false).Should().Be(Strings.Chart_Note_NoApplicationFrames);
        FrametimeChart.NoteFor(gapped, displayed: false, sensors: true).Should().Contain("1 gap");
        FrametimeChart.NoteFor(null, displayed: true, sensors: true).Should().BeEmpty();
        return 0;
    });

    [Fact]
    public void TheSensorsAxisSaysWhereItsZeroIs() => InEnglish(() =>
    {
        SensorsChart.TimeAxisLabel(SessionSeriesLoader.Decode(Blob([0, 10], [4, 0]) with { FirstPresentMs = 0 }, [], [])).Should().Be(Strings.Chart_Axis_TimeFromFirstFrame);
        SensorsChart.TimeAxisLabel(SessionSeries.SensorsOnly([])).Should().Be(Strings.Chart_Axis_TimeFromStart);
        SensorsChart.TimeAxisLabel(null).Should().Be(Strings.Chart_Axis_TimeFromStart);
        return 0;
    });

    /// <summary>
    /// beta.8: the light theme drew the GPU's power and the CPU's temperature in one gold, and the adapter's VRAM and the RAM
    /// likewise — two lines a reader could not tell apart. Every pair of lines on one plot is now ≥ 60 apart in RGB.
    /// </summary>
    [Theory]
    [InlineData("ChartPalette.Dark.xaml")]
    [InlineData("ChartPalette.Light.xaml")]
    public void TheSensorsChartsLinesAreToldApartInEachTheme(string file)
    {
        Dictionary<string, (int R, int G, int B)> palette = Palette(file);

        foreach (string[] plot in new[] { SensorsChart.TempsPlot.Select(static s => s.PaletteKey).ToArray(), SensorsChart.MemoryPlot.Select(static s => s.PaletteKey).ToArray() })
        {
            for (int i = 0; i < plot.Length; i++)
            {
                for (int j = i + 1; j < plot.Length; j++)
                {
                    Distance(palette[plot[i]], palette[plot[j]]).Should().BeGreaterThanOrEqualTo(60, $"{plot[i]} and {plot[j]} are two lines on one plot in {file}");
                }
            }
        }
    }

    private static readonly XNamespace _x = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace _p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static Dictionary<string, (int R, int G, int B)> Palette(string file)
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "FrameLedger.slnx")))
        {
            dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("FrameLedger.slnx not found above the test binary");
        }

        XDocument doc = XDocument.Load(Path.Combine(dir, "src", "FrameLedger.App", "Styles", file));
        return doc.Root!.Elements(_p + "Color").ToDictionary(
            e => (string)e.Attribute(_x + "Key")!,
            e => (Convert.ToInt32(e.Value.Substring(3, 2), 16), Convert.ToInt32(e.Value.Substring(5, 2), 16), Convert.ToInt32(e.Value.Substring(7, 2), 16)),
            StringComparer.Ordinal);
    }

    private static double Distance((int R, int G, int B) a, (int R, int G, int B) b) =>
        Math.Sqrt(((a.R - b.R) * (a.R - b.R)) + ((a.G - b.G) * (a.G - b.G)) + ((a.B - b.B) * (a.B - b.B)));

    private static FrameBlobs Blob(float[] frametimes, byte[] flags) => new()
    {
        Codec = SeriesCodec.Tag,
        SampleCount = frametimes.Length,
        FrameTimes = SeriesCodec.EncodeFloat32(frametimes),
        FrameFlags = SeriesCodec.EncodeBytes(flags),
    };

    private static void InEnglish(Func<int> body)
    {
        System.Globalization.CultureInfo? previous = Strings.Culture;
        Strings.Culture = System.Globalization.CultureInfo.GetCultureInfo("en");
        try
        {
            body();
        }
        finally
        {
            Strings.Culture = previous;
        }
    }
}
