// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;

namespace FrameLedger.App.Charts;

/// <summary>
/// Where the change markers' numbered badges stand in the band above a trend chart (beta.30, owner decision D70): each over
/// its line, inside the data area's width, and never over another. Badges too close for their lines stand aside, in their
/// lines' order (the chart joins each to its line); when a badge would stand farther than <see cref="MaxShift"/> from its
/// line, or the badges cannot stand side by side at all, the two nearest neighbours become one badge for the run
/// (<c>3–5</c>), and the layout starts again. Pure: the chart measures the labels, this places them.
/// </summary>
internal static class ChangeBadgeLayout
{
    /// <summary>How far a badge may stand from its line before its number stops reading as that line's — about one badge.</summary>
    internal const float MaxShift = 22;

    /// <summary>A marker's line, at its pixel x.</summary>
    internal readonly record struct Anchor(float X, ChangeMarker Marker);

    /// <summary>A badge: its centre and width, its label (a number, or a run's first and last), and the lines it stands for.</summary>
    internal sealed record Placed(float Center, float Width, string Label, IReadOnlyList<Anchor> Members)
    {
        public float Left => Center - (Width / 2);

        public float Right => Center + (Width / 2);

        /// <summary>Where the badge would stand with room to spare: over its line, or the middle of its run's lines.</summary>
        public float AnchorX => (Members[0].X + Members[^1].X) / 2;
    }

    /// <summary>
    /// The badges for <paramref name="anchors"/> between <paramref name="left"/> and <paramref name="right"/>, at least
    /// <paramref name="gap"/> apart; <paramref name="widthOf"/> gives a label's badge width, padding included. Empty when not
    /// even one badge fits.
    /// </summary>
    public static IReadOnlyList<Placed> Place(IReadOnlyList<Anchor> anchors, float left, float right, float gap, Func<string, float> widthOf)
    {
        ArgumentNullException.ThrowIfNull(anchors);
        ArgumentNullException.ThrowIfNull(widthOf);
        List<List<Anchor>> runs = [.. anchors.OrderBy(static a => a.X).Select(static a => new List<Anchor> { a })];
        while (runs.Count > 0)
        {
            Placed[] placed = [.. runs.Select(run => new Placed(0, widthOf(Label(run)), Label(run), run))];
            float needed = placed.Sum(static p => p.Width) + (gap * (placed.Length - 1));
            bool fits = needed <= right - left;
            if (fits)
            {
                Spread(placed, left, right, gap);
                if (runs.Count == 1 || placed.All(static p => Math.Abs(p.Center - p.AnchorX) <= MaxShift))
                {
                    return placed;
                }
            }
            else if (runs.Count == 1)
            {
                return [];
            }

            MergeClosest(runs);
        }

        return [];
    }

    /// <summary>A run's label: its number, or its first and last numbers (the runs keep the badges' order, so they are consecutive).</summary>
    private static string Label(List<Anchor> run) => run.Count == 1
        ? run[0].Marker.NumberText
        : string.Create(CultureInfo.InvariantCulture, $"{run[0].Marker.NumberText}–{run[^1].Marker.NumberText}");

    /// <summary>
    /// Each badge as near its line as the others allow: a pass from the left keeps them apart and inside the left edge, a
    /// pass from the right keeps them inside the right edge. With room for all of them, the two passes cannot overlap them.
    /// </summary>
    private static void Spread(Placed[] placed, float left, float right, float gap)
    {
        float edge = left - gap;
        for (int i = 0; i < placed.Length; i++)
        {
            float half = placed[i].Width / 2;
            float center = Math.Max(placed[i].AnchorX, edge + gap + half);
            placed[i] = placed[i] with { Center = center };
            edge = center + half;
        }

        edge = right + gap;
        for (int i = placed.Length - 1; i >= 0; i--)
        {
            float half = placed[i].Width / 2;
            float center = Math.Min(placed[i].Center, edge - gap - half);
            placed[i] = placed[i] with { Center = center };
            edge = center - half;
        }
    }

    /// <summary>The two neighbouring runs whose lines stand closest become one (the leftmost pair on a tie).</summary>
    private static void MergeClosest(List<List<Anchor>> runs)
    {
        int best = 0;
        float bestGap = float.MaxValue;
        for (int i = 0; i + 1 < runs.Count; i++)
        {
            float between = runs[i + 1][0].X - runs[i][^1].X;
            if (between < bestGap)
            {
                bestGap = between;
                best = i;
            }
        }

        runs[best].AddRange(runs[best + 1]);
        runs.RemoveAt(best + 1);
    }
}
