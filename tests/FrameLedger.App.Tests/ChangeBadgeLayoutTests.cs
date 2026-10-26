// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FluentAssertions;
using FrameLedger.App.Charts;

namespace FrameLedger.App.Tests;

/// <summary>
/// beta.30 (owner decision D70): where the numbered change badges stand above a trend chart — over their lines, inside the
/// data area's width, never over another, a run of crowded days one badge — as the pure layout under the chart.
/// </summary>
public sealed class ChangeBadgeLayoutTests
{
    private const float _gap = 3;

    /// <summary>A badge's width for its label: about a 7 px digit, 8 px of padding.</summary>
    private static float Width(string label) => (7 * label.Length) + 8;

    /// <summary>Lines at <paramref name="xs"/>, numbered 1, 2, … from the left, as the markers are by date.</summary>
    private static ChangeBadgeLayout.Anchor[] Anchors(params float[] xs) =>
        [.. xs.Order().Select(static (x, i) => new ChangeBadgeLayout.Anchor(x, new ChangeMarker(i + 1, DateTimeOffset.UnixEpoch.AddDays(i), ["GPU driver: a → b"])))];

    [Fact]
    public void ABadgeWithRoomStandsOverItsLine()
    {
        ChangeBadgeLayout.Placed badge = ChangeBadgeLayout.Place(Anchors(300), 50, 600, _gap, Width).Single();

        badge.Center.Should().Be(300);
        badge.Label.Should().Be("1");
    }

    [Fact]
    public void BadgesTooCloseForTheirLinesStandAsideInTheirOrder()
    {
        IReadOnlyList<ChangeBadgeLayout.Placed> placed = ChangeBadgeLayout.Place(Anchors(300, 301.5f), 50, 600, _gap, Width);

        placed.Select(static p => p.Label).Should().Equal("1", "2");
        (placed[1].Left - placed[0].Right).Should().BeGreaterThanOrEqualTo(_gap - 0.001f, "two badges never touch");
        placed.Should().OnlyContain(static p => Math.Abs(p.Center - p.AnchorX) <= ChangeBadgeLayout.MaxShift, "each still reads as its line's");
    }

    [Theory]
    [InlineData(50f)]
    [InlineData(600f)]
    public void ABadgeOnTheDataAreasEdgeStaysInsideIt(float x)
    {
        ChangeBadgeLayout.Placed badge = ChangeBadgeLayout.Place(Anchors(x), 50, 600, _gap, Width).Single();

        badge.Left.Should().BeGreaterThanOrEqualTo(50);
        badge.Right.Should().BeLessThanOrEqualTo(600);
    }

    [Fact]
    public void ACrowdedRunOfDaysIsOneBadgeForTheRun()
    {
        IReadOnlyList<ChangeBadgeLayout.Placed> placed = ChangeBadgeLayout.Place(Anchors([.. Enumerable.Range(0, 10).Select(static i => 300 + (i * 0.5f))]), 50, 600, _gap, Width);

        placed.Should().ContainSingle().Which.Label.Should().Be("1–10");
        placed[0].Members.Should().HaveCount(10);
    }

    [Fact]
    public void NoBadgeIsDrawnWhereNotEvenOneFits() =>
        ChangeBadgeLayout.Place(Anchors(10), 0, 12, _gap, Width).Should().BeEmpty();

    [Fact]
    public void AtAnyWidthEveryNumberIsShownOnceInsideTheAreaAndNoBadgeIsOverAnother()
    {
        // Thirty lines at fixed, uneven places — some a fraction of a pixel apart, some far — scaled to every width.
        float[] xs = [.. Enumerable.Range(0, 30).Select(static i => ((i * i) % 97) + (i * 3.7f)).Order()];
        float span = xs[^1];
        for (float width = 20; width <= 2000; width += 13)
        {
            float scale = width / span;
            IReadOnlyList<ChangeBadgeLayout.Placed> placed = ChangeBadgeLayout.Place(Anchors([.. xs.Select(x => x * scale)]), 0, width, _gap, Width);
            if (placed.Count == 0)
            {
                width.Should().BeLessThan(Width("1–30"), "only a chart narrower than the one badge for every day draws none");
                continue;
            }

            placed.Should().OnlyContain(p => p.Left >= -0.001f && p.Right <= width + 0.001f, $"inside the data area at {width} px");
            for (int i = 1; i < placed.Count; i++)
            {
                (placed[i].Left - placed[i - 1].Right).Should().BeGreaterThanOrEqualTo(_gap - 0.001f, $"no badge over another at {width} px");
            }

            placed.SelectMany(static p => p.Members).Select(static m => m.Marker.Number).Should().Equal(Enumerable.Range(1, 30), $"every number once, in order, at {width} px");
            foreach (ChangeBadgeLayout.Placed run in placed.Where(static p => p.Members.Count > 1))
            {
                run.Label.Should().Be(run.Members[0].Marker.NumberText + "–" + run.Members[^1].Marker.NumberText, "a run is named by its first and last day");
            }
        }
    }
}
