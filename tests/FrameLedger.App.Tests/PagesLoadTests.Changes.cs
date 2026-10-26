// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.App.Charts;
using ScottPlot;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.30 (the owner's report, 2026-10-26: "Driver GPU: 32.0.16.1742 → 32.0.16.2101" cut off at the top of the chart; owner
/// decision D70): the trend chart's change markers drawn for real — every numbered badge whole inside the image, above the data
/// area and inside its width, none over another, every number shown — at the Trend's size, the overview's, a narrow window's,
/// with markers on the data area's edges and with thirty days in a row; and the tooltip near a line or over a badge.
/// </summary>
public sealed partial class PagesLoadTests
{
    private static readonly DateTimeOffset _first = new(new DateTime(2025, 10, 1, 12, 0, 0, DateTimeKind.Local));

    /// <summary>A year of sessions, a week apart.</summary>
    private static TrendPoint[] Year() =>
        [.. Enumerable.Range(0, 53).Select(static i => new TrendPoint(_first.AddDays(i * 7), 58 + (i % 4), i + 1, false))];

    private static ChangeMarker Change(int number, DateTimeOffset at) => new(number, at, ["GPU driver: 32.0.16.1742 → 32.0.16.2101"]);

    [Theory]
    [InlineData(640, 320, false)]
    [InlineData(640, 220, false)]
    [InlineData(640, 220, true)]
    [InlineData(300, 220, false)]
    public async Task EveryChangeBadgeIsWholeInsideTheChartAndNoneIsOverAnother(int width, int height, bool tight)
    {
        TrendPoint[] points = Year();
        ChangeMarker[] markers = [Change(1, points[0].At), Change(2, _first.AddDays(120)), Change(3, _first.AddDays(121)), Change(4, points[^1].At)];

        Drawn drawn = await OnStaAsync(() => Draw(points, markers, width, height, tight));

        AssertWhole(drawn, width, height, numbers: 4);
    }

    [Fact]
    public async Task ThirtyDaysInARowStillShowEveryNumberOnceAndNoBadgeOverAnother()
    {
        TrendPoint[] points = Year();
        ChangeMarker[] markers = [.. Enumerable.Range(0, 30).Select(static i => Change(i + 1, _first.AddDays(200 + i)))];

        Drawn drawn = await OnStaAsync(() => Draw(points, markers, 300, 220, tight: false));

        AssertWhole(drawn, 300, 220, numbers: 30);
        drawn.Labels.Should().Contain(static l => l.Contains('–', StringComparison.Ordinal), "thirty days on a 300 px chart cannot stand side by side");
    }

    [Fact]
    public async Task PointingAtABadgeOrNearItsLineNamesItsChanges()
    {
        TrendPoint[] points = Year();
        ChangeMarker[] markers = [Change(1, _first.AddDays(100)), Change(2, _first.AddDays(250))];

        (string? onBadge, string? nearLine, string? farFromLines, string? overAxis) = await OnStaAsync(() =>
        {
            var chart = new TrendChart();
            chart.Show(points, markers, "FPS");
            using ScottPlot.Image image = chart.ScottPlot.GetImage(640, 320);
            PixelRect data = chart.ScottPlot.LastRender.DataRect;
            float line = chart.ScottPlot.GetPixel(new Coordinates(markers[1].At.LocalDateTime.ToOADate(), 0)).X;
            return (
                chart.ToolTipAt(chart.Badges[0].Rect.Center, 14),
                chart.ToolTipAt(new Pixel(line + 10, data.VerticalCenter), 14),
                chart.ToolTipAt(new Pixel(line + 40, data.VerticalCenter), 14),
                chart.ToolTipAt(new Pixel(line, data.Bottom + 20), 14));
        });

        onBadge.Should().Be(markers[0].ToolTipText);
        nearLine.Should().Be(markers[1].ToolTipText, "within reach of its line");
        farFromLines.Should().BeNull();
        overAxis.Should().BeNull("under the data area is the date axis, not the line");
    }

    private sealed record Drawn(List<PixelRect> Rects, List<string> Labels, PixelRect Data, IReadOnlyList<ChangeMarker> Markers);

    private static Drawn Draw(TrendPoint[] points, ChangeMarker[] markers, int width, int height, bool tight)
    {
        var chart = new TrendChart();
        chart.Show(points, markers, "FPS");
        if (tight)
        {
            // The first and last markers exactly on the data area's edges.
            chart.ScottPlot.Axes.SetLimitsX(points[0].At.LocalDateTime.ToOADate(), points[^1].At.LocalDateTime.ToOADate());
        }

        using ScottPlot.Image image = chart.ScottPlot.GetImage(width, height);
        return new Drawn([.. chart.Badges.Select(static b => b.Rect)], [.. chart.Badges.Select(static b => b.Label)], chart.ScottPlot.LastRender.DataRect, chart.Markers);
    }

    private static void AssertWhole(Drawn drawn, int width, int height, int numbers)
    {
        drawn.Markers.Should().HaveCount(numbers, "the chart keeps the page's numbering");
        drawn.Rects.Should().NotBeEmpty();
        foreach (PixelRect rect in drawn.Rects)
        {
            rect.Left.Should().BeGreaterThanOrEqualTo(0);
            rect.Top.Should().BeGreaterThanOrEqualTo(0, "the badge's top is inside the image — the cut the owner saw");
            rect.Right.Should().BeLessThanOrEqualTo(width);
            rect.Bottom.Should().BeLessThanOrEqualTo(drawn.Data.Top, "above the data area, in the band kept for it");
            rect.Left.Should().BeGreaterThanOrEqualTo(drawn.Data.Left - 0.5f);
            rect.Right.Should().BeLessThanOrEqualTo(drawn.Data.Right + 0.5f);
        }

        drawn.Data.Bottom.Should().BeLessThanOrEqualTo(height);
        List<PixelRect> ordered = [.. drawn.Rects.OrderBy(static r => r.Left)];
        for (int i = 1; i < ordered.Count; i++)
        {
            ordered[i].Left.Should().BeGreaterThanOrEqualTo(ordered[i - 1].Right, "no badge over another");
        }

        List<int> shown = [.. drawn.Labels.SelectMany(static l =>
        {
            string[] ends = l.Split('–');
            int from = int.Parse(ends[0], System.Globalization.CultureInfo.InvariantCulture);
            int to = int.Parse(ends[^1], System.Globalization.CultureInfo.InvariantCulture);
            return Enumerable.Range(from, to - from + 1);
        })];
        shown.Should().Equal(Enumerable.Range(1, numbers), "every number once, in order");
    }
}
