// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.App.Charts;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Sessions;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.14 (D49), the charts' half: the render scale over a session, and the card's clocks with the time its limits held it
/// back. The arithmetic is pure and pinned here; the two charts are drawn on the STA thread to show they draw it.
/// </summary>
public sealed class RenderScaleAndClocksTests
{
    private static readonly SessionRow _row = new()
    {
        SessionGuid = Guid.NewGuid(),
        GameId = 1,
        SnapshotId = 1,
        StartedAt = DateTimeOffset.UnixEpoch,
        EndedAt = DateTimeOffset.UnixEpoch.AddMinutes(1),
        QpcEpoch = 0,
        QpcFrequency = 1,
        Tier = CaptureTier.Hooked,
        Mode = CaptureMode.Launch,
        ExitStatus = ExitStatus.Normal,
        RenderW = 1707,
        RenderH = 960,
        OutputW = 2560,
        OutputH = 1440,
    };

    /// <summary>Presents 0.1 s apart; <paramref name="renderRes"/> is the blob's four values per present, or none.</summary>
    private static SessionSeries Frames(int presents, ushort[]? renderRes = null) => new()
    {
        TimesS = [.. Enumerable.Range(0, presents).Select(static i => i * 0.1)],
        FrameTimesMs = new float[presents],
        Generated = new bool[presents],
        Gap = new bool[presents],
        RenderRes = renderRes,
        AppTimesS = [],
        AppFrameTimesMs = [],
        AppPresentIndex = [],
        Segments = [],
        Sensors = [],
    };

    [Fact]
    public void TheScaleIsThePerAxisShareOfTheOutput()
    {
        RenderScaleSeries.Scale(1707, 960, 2560, 1440)!.Value.Should().BeApproximately(66.67, 0.01, "DLSS Quality");
        RenderScaleSeries.Scale(2560, 1440, 2560, 1440).Should().Be(100, "DLAA renders at the output");
        RenderScaleSeries.Scale(0, 960, 2560, 1440).Should().BeNull("0 is the writer's unknown");
    }

    [Fact]
    public void ABlobIsDrawnWhereTheScaleChangesAndToTheEnd()
    {
        // Quality for three presents, Performance for two, Quality again for three: three steps and the closing point.
        ushort[] quality = [1707, 960, 2560, 1440];
        ushort[] performance = [1280, 720, 2560, 1440];
        ushort[] blob = [.. quality, .. quality, .. quality, .. performance, .. performance, .. quality, .. quality, .. quality];

        (double[] xs, double[] ys) = RenderScaleSeries.Of(Frames(8, blob), _row);

        xs.Should().Equal(0, 0.30000000000000004, 0.5, 0.7000000000000001);
        ys.Select(static y => Math.Round(y, 1)).Should().Equal(66.7, 50, 66.7, 66.7);
    }

    [Fact]
    public void WithoutABlobTheRowsExtentIsAFlatLineAndWithoutEitherNothing()
    {
        (double[] xs, double[] ys) = RenderScaleSeries.Of(Frames(11), _row);
        xs.Should().Equal(0, 1.0);
        ys.Should().AllSatisfy(static y => y.Should().BeApproximately(66.67, 0.01));

        RenderScaleSeries.Of(Frames(11), _row with { RenderW = null }).Xs.Should().BeEmpty("the upscaler's parameters were never measured");
        RenderScaleSeries.Of(null, _row).Xs.Should().BeEmpty("a session that was not hooked has no frames");
    }

    [Fact]
    public void ALimitIsShadedFromItsFirstTickToTheNextTickThatWasNot()
    {
        var limit = new SensorSeries(SensorSeriesCatalog.GpuPowerLimit, [0, 1, 2, 3, 4, 5], [0, 100, 100, 0, 100, 100]);

        SensorsChart.LimitRuns(limit).Should().Equal((1.0, 3.0), (4.0, 6.0));
        SensorsChart.LimitRuns(new SensorSeries(SensorSeriesCatalog.GpuPowerLimit, [0, 1], [0, 0])).Should().BeEmpty();
    }

    [Fact]
    public async Task TheClocksPlotAndTheRenderScaleChartDrawWhatTheSessionHas()
    {
        SessionSeries sensors = SessionSeries.SensorsOnly(
        [
            new SensorSeries(SensorSeriesCatalog.GpuCoreClock, [0, 1, 2], [2600, 2650, 2700]),
            new SensorSeries(SensorSeriesCatalog.GpuFan, [0, 1, 2], [1400, 1450, 1500]),
            new SensorSeries(SensorSeriesCatalog.GpuPowerLimit, [0, 1, 2], [100, 0, 100]),
        ]);

        (int lines, int spans, int scalePoints, int notMeasured) = await PagesLoadTests.OnStaAsync(() =>
        {
            var chart = new SensorsChart();
            chart.Show(sensors);
            var scale = new RenderScaleChart();
            scale.Show(Frames(11), _row);
            int drawn = scale.DrawnPoints;
            scale.Show(Frames(11), _row with { RenderW = null });
            return (chart.DrawnSeries, chart.LimitSpansDrawn, drawn, scale.DrawnPoints);
        });

        lines.Should().Be(2, "the core clock and the fan; this session stored no memory clock and no temperature");
        spans.Should().Be(2);
        scalePoints.Should().Be(2, "a flat line from the row's extent");
        notMeasured.Should().Be(0, "and nothing where it was never measured");
    }
}
