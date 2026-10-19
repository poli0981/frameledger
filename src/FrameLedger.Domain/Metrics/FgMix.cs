// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Metrics;

/// <summary>
/// Every frame-generation state a session held, with the share of its presenting time each one took — the owner's
/// "×2: 10 %, ×3: 25.75 %, ×4: 35.33 %, no frame generation: …" (beta.24, decision D62).
/// </summary>
/// <remarks>
/// <para>
/// <b>The steady state's windows, all of them.</b> <see cref="FgSteadyState"/> keeps the state that held the most time;
/// this takes it, then the state that holds the most of what is left, and so on, by the same rule
/// (<see cref="FgSlices.DominantState"/>) over the same five-second windows (<see cref="FgSlices.Cut"/>), until no group of
/// agreeing windows lasts <see cref="FgSteadyState.MinSeconds"/>. So the longest state of the mix is the steady state.
/// </para>
/// <para>
/// <b>Never an average across states</b> (CLAUDE.md rule 6): each state keeps its own factor, its own Native and
/// Displayed rates and its share. The factor is the pooled ratio, never rounded to a whole multiplier: a session at ×4
/// reads 3.99 or 4.11 because that is what was counted.
/// </para>
/// <para>
/// <b>Rows add up to the whole</b>: the share's denominator is every window of the claimed span — the steady state's own
/// denominator — and every window lands in exactly one row. States come first, by rising factor, then
/// <see cref="FgMixKind.None"/>, then <see cref="FgMixKind.Unclassified"/>.
/// </para>
/// <para>
/// <b>A window the count does not describe is not classified</b> (beta.26): fewer than <see cref="FgWindow.MinPerBucket"/>
/// application frames, or a ratio above <see cref="FgWindow.GenerationCeiling"/> (<see cref="FgSlices.Slice.IsReadable"/>,
/// <see cref="FgSlices.Slice.IsActive"/>).
/// </para>
/// </remarks>
public sealed record FgMix(IReadOnlyList<FgMixRow> Rows)
{
    /// <summary>
    /// The mix over <paramref name="all"/> from its first sample that counted application frames, or null when there is
    /// nothing to split: no counted span, a span of no length, or no frame-generation state at all.
    /// </summary>
    /// <param name="all">The session's samples, every stream, in drained order.</param>
    /// <param name="qpcFrequency">Ticks per second of the samples' clock.</param>
    /// <param name="noneAllowed">
    /// Whether a counted <c>none</c> may be believed: false on the shape <c>FgLadder.WithholdNone</c> withholds, where
    /// generated presents can be invisible to the hook — those windows are then unclassified, never "no frame generation".
    /// </param>
    public static FgMix? From(IReadOnlyList<FrameSample> all, long qpcFrequency, bool noneAllowed)
    {
        ArgumentNullException.ThrowIfNull(all);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(qpcFrequency);

        int start = RecordWindow.ClaimedSuffixStart(all, MeasuredFields.FgCounts);
        if (start >= all.Count)
        {
            return null;
        }

        List<FgSlices.Slice> slices = FgSlices.Cut(all, start, qpcFrequency);
        double total = slices.Sum(static s => s.Seconds);
        if (total <= 0)
        {
            return null;
        }

        var active = new List<FgSlices.Slice>();
        var none = new List<FgSlices.Slice>();
        var unclassified = new List<FgSlices.Slice>();
        foreach (FgSlices.Slice s in slices)
        {
            List<FgSlices.Slice> into = s.IsActive ? active
                : noneAllowed && s.IsReadable && s.Factor <= FgWindow.NoneCeiling ? none
                : unclassified;
            into.Add(s);
        }

        List<List<FgSlices.Slice>> states = States(active, unclassified);
        if (states.Count == 0)
        {
            return null;
        }

        var rows = new List<FgMixRow>();
        rows.AddRange(states.OrderBy(static s => Pooled(s)).Select(s => Row(FgMixKind.Generated, s, total)));

        // A last window can end on its first sample and so last no time: it changes no share and makes no row.
        if (none.Sum(static s => s.Seconds) > 0)
        {
            rows.Add(Row(FgMixKind.None, none, total));
        }

        if (unclassified.Sum(static s => s.Seconds) > 0)
        {
            rows.Add(Row(FgMixKind.Unclassified, unclassified, total));
        }

        return new FgMix(rows);
    }

    /// <summary>
    /// The steady state, then the steady state of what is left, until no group of agreeing windows lasts
    /// <see cref="FgSteadyState.MinSeconds"/>; what is left then goes to <paramref name="unclassified"/>.
    /// </summary>
    private static List<List<FgSlices.Slice>> States(List<FgSlices.Slice> active, List<FgSlices.Slice> unclassified)
    {
        var states = new List<List<FgSlices.Slice>>();
        while (active.Count > 0)
        {
            List<FgSlices.Slice> state = FgSlices.DominantState(active);
            if (state.Sum(static s => s.Seconds) < FgSteadyState.MinSeconds)
            {
                // The longest group left is a transition, so every group left is: none of them is a state.
                unclassified.AddRange(active);
                break;
            }

            states.Add(state);
            active.RemoveAll(state.Contains);
        }

        return states;
    }

    private static double Pooled(List<FgSlices.Slice> slices) =>
        slices.Sum(static s => s.Displayed) / (double)slices.Sum(static s => s.Tokens);

    private static FgMixRow Row(FgMixKind kind, List<FgSlices.Slice> slices, double total)
    {
        double seconds = slices.Sum(static s => s.Seconds);
        long displayed = slices.Sum(static s => s.Displayed);
        long tokens = slices.Sum(static s => s.Tokens);
        bool rated = kind != FgMixKind.Unclassified;
        return new FgMixRow(
            kind,
            kind == FgMixKind.Generated ? displayed / (double)tokens : null,
            rated ? tokens / seconds : null,
            rated ? displayed / seconds : null,
            seconds,
            seconds / total,
            slices.Count);
    }
}
