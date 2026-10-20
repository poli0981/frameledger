// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Domain.Metrics;

/// <summary>
/// The five-second windows frame generation is read over — the live card's own — and the one way both
/// <see cref="FgSteadyState"/> and <see cref="FgMix"/> group them into states, so the two can never disagree about what a
/// window or a state is (beta.24: cut out of <see cref="FgSteadyState"/> unchanged when <see cref="FgMix"/> needed it too).
/// </summary>
internal static class FgSlices
{
    /// <summary>One window: its displayed presents, application-frame tokens, samples, and seconds.</summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct Slice(long Displayed, long Tokens, int Samples, double Seconds)
    {
        /// <summary>Read only on windows that have tokens.</summary>
        public double Factor => Displayed / (double)Tokens;

        /// <summary>
        /// A window a ratio can be read from: <see cref="FgWindow.MinPerBucket"/> presents AND as many application frames
        /// (beta.26). Presents alone let a window with ONE token through, and its ratio was the presents themselves: Silent
        /// Hill 2's ten seconds at one token a window read ×1336.0, a state the mix listed and the DLSS line cited.
        /// </summary>
        public bool IsReadable => Samples >= FgWindow.MinPerBucket && Tokens >= FgWindow.MinPerBucket;

        /// <summary>
        /// Generating: readable, at least <see cref="FgWindow.ActiveThreshold"/>, no more than
        /// <see cref="FgWindow.GenerationCeiling"/> — above it the ratio is no frame generation's — and near a whole
        /// multiplier (<see cref="FgWindow.IsNearWholeMultiplier"/>, beta.27): a window between two changed state inside
        /// itself, and is no state's.
        /// </summary>
        public bool IsActive => IsReadable && Displayed >= Tokens * FgWindow.ActiveThreshold && Factor <= FgWindow.GenerationCeiling
            && FgWindow.IsNearWholeMultiplier(Factor);
    }

    /// <summary>
    /// The set of mutually agreeing windows of <paramref name="active"/> (none empty, all active) that holds the most time:
    /// every window is tried as the centre, the winner pooled and the set re-drawn once around the pooled factor, so the
    /// centre's own noise does not decide membership. Never empty: the pooled factor is a weighted mean of members within
    /// <see cref="FgSteadyState.StateTolerance"/> of the centre, and a mean always has a member that close to itself.
    /// </summary>
    public static List<Slice> DominantState(List<Slice> active)
    {
        double centre = active[0].Factor;
        double best = -1;
        foreach (Slice candidate in active)
        {
            double seconds = Within(active, candidate.Factor).Sum(static s => s.Seconds);
            if (seconds > best)
            {
                best = seconds;
                centre = candidate.Factor;
            }
        }

        return [.. Within(active, Pooled([.. Within(active, centre)]))];
    }

    private static IEnumerable<Slice> Within(List<Slice> active, double factor) =>
        active.Where(s => Math.Abs(s.Factor - factor) <= factor * FgSteadyState.StateTolerance);

    private static double Pooled(List<Slice> slices) =>
        slices.Sum(static s => s.Displayed) / (double)Math.Max(1, slices.Sum(static s => s.Tokens));

    /// <summary>
    /// Fixed wall-clock windows from <paramref name="start"/>'s sample. A window's seconds are its nominal length, except
    /// the last, which ends at the last sample; a window with no sample in it (the title minimised) is not a window.
    /// </summary>
    public static List<Slice> Cut(IReadOnlyList<FrameSample> all, int start, long qpcFrequency)
    {
        ulong origin = all[start].Qpc;
        ulong last = all[^1].Qpc;
        double ticksPerWindow = FgSteadyState.WindowSeconds * qpcFrequency;
        var slices = new List<Slice>();
        long index = -1;
        long displayed = 0;
        long tokens = 0;
        int samples = 0;

        void Close()
        {
            if (samples == 0)
            {
                return;
            }

            // A window's length is nominal, except the last, which ends at the last sample (and may be zero long).
            double windowStart = origin + (index * ticksPerWindow);
            slices.Add(new Slice(displayed, tokens, samples, Math.Clamp((last - windowStart) / qpcFrequency, 0, FgSteadyState.WindowSeconds)));
        }

        for (int i = start; i < all.Count; i++)
        {
            FrameSample s = all[i];
            long at = (long)(Math.Max(0d, (double)s.Qpc - origin) / ticksPerWindow);
            if (at != index)
            {
                Close();
                index = at;
                displayed = 0;
                tokens = 0;
                samples = 0;
            }

            displayed += 1 + (s.Claims(MeasuredFields.DxgiPresents) ? s.DxgiUnseen : 0);
            tokens += s.FgEvaluations;
            samples++;
        }

        Close();
        return slices;
    }
}
