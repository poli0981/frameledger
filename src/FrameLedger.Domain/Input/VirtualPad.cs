// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Input;

/// <summary>
/// Whether two entries of a session's controller list are one pad seen twice (<c>03_METRICS</c> §Input). With Steam
/// Input between a controller and a game, Windows shows the controller the player holds and an Xbox pad Steam presents
/// for it; both move together. Counted as two, a session would have twice the controllers and a share for a maker
/// nobody held.
/// </summary>
public static class VirtualPad
{
    /// <summary>How much of the busier entry's time the other must share for the two to be one pad.</summary>
    public const double MergeAt = 0.9;

    /// <summary>The least time in use the two must have: a few seconds moving together prove nothing.</summary>
    public const long MinimumActiveMs = 10_000;

    /// <summary>
    /// How much two controllers were in use at the same moments: the time both were in use over the time the busier of
    /// the two was, 0 to 1. Each array holds a controller's milliseconds in use for every tick of the session, tick for
    /// tick.
    /// </summary>
    public static double Coincidence(ReadOnlySpan<int> a, ReadOnlySpan<int> b)
    {
        long together = 0;
        long sumA = 0;
        long sumB = 0;
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
        {
            together += Math.Min(a[i], b[i]);
        }

        foreach (int ms in a)
        {
            sumA += ms;
        }

        foreach (int ms in b)
        {
            sumB += ms;
        }

        long busier = Math.Max(sumA, sumB);
        return busier <= 0 ? 0 : (double)together / busier;
    }

    /// <summary>Whether the two are one pad: they were in use for long enough, and at the same moments.</summary>
    public static bool IsSamePad(ReadOnlySpan<int> a, ReadOnlySpan<int> b)
    {
        long sumA = 0;
        long sumB = 0;
        foreach (int ms in a)
        {
            sumA += ms;
        }

        foreach (int ms in b)
        {
            sumB += ms;
        }

        return Math.Min(sumA, sumB) >= MinimumActiveMs && Coincidence(a, b) >= MergeAt;
    }
}
