using System.Runtime.InteropServices;

namespace FrameLedger.Domain.Metrics;

/// <summary>
/// The frame-generation state a session spent most of its generating time in, when the session as a whole had more
/// than one (<c>03_METRICS</c> §Frame Generation, owner decision 2026-09-17).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists.</b> <see cref="FgWindow"/> refuses a session-level factor when any of its eight buckets departs
/// from the whole — correctly: a session that ran its menus at ×1 and its gameplay at ×4 averages to a number no
/// configuration ever had (Black Myth: Wukong, 2026-09-16: 2.22 over a session whose gameplay read ×4.1 for 60 % of
/// it). But the refusal threw the gameplay's own number away with the average, and on real play every session opens
/// with a splash, a menu or a loading screen — so the shipped Agent published a factor on one hooked session in nine
/// while the live card, which looks at five seconds at a time, showed it throughout. Measured on those nine sessions'
/// stored per-frame counts: inside gameplay the five-second factor read 2.00, 3.00, 4.00 to two decimals.
/// </para>
/// <para>
/// <b>What it is.</b> The claimed span cut into <see cref="WindowSeconds"/>-second windows — the live card's own window —
/// each classified by its own <c>presents ÷ tokens</c>; the ACTIVE windows (≥ <see cref="FgWindow.ActiveThreshold"/>)
/// that agree with one another within <see cref="StateTolerance"/> form a state, and the state holding the most time
/// is this record, with the share of the presenting time it covers. ×3 and ×4 are 25 % apart and never pool; a
/// factor that wanders (a dynamic multiplier) forms no state long enough and the refusal stands alone.
/// </para>
/// <para>
/// <b>What it is not.</b> Not a session average, never blended with the windows outside it, and never published
/// without its <see cref="Share"/> beside it — the reader is told which part of the session the number describes.
/// <see cref="NativeFps"/> and <see cref="DisplayedFps"/> are counted over the state's own windows, independently,
/// as <see cref="FgWindow"/> counts its own.
/// </para>
/// </remarks>
/// <param name="Factor">Pooled <c>Σ displayed ÷ Σ tokens</c> over the state's windows.</param>
/// <param name="NativeFps"><c>Σ tokens ÷ seconds</c> over those windows.</param>
/// <param name="DisplayedFps"><c>Σ displayed ÷ seconds</c> over those windows.</param>
/// <param name="Seconds">Time the state's windows cover.</param>
/// <param name="Share">That time as a fraction of the time the title was presenting (every non-empty window).</param>
/// <param name="Windows">How many windows formed the state.</param>
public sealed record FgSteadyState(double Factor, double NativeFps, double DisplayedFps, double Seconds, double Share, int Windows)
{
    /// <summary>The live card's window (<c>SessionProgressCalculator.WindowSeconds</c>): what the user watched is what is cut.</summary>
    public const double WindowSeconds = 5;

    /// <summary>How far a window's factor may sit from the state's before it belongs to another state. ×3 vs ×4 is 25 %.</summary>
    public const double StateTolerance = 0.10;

    /// <summary>A state shorter than this is a transition, not a configuration.</summary>
    public const double MinSeconds = 10;

    /// <summary>A state covering less of the presenting time than this does not speak for the session.</summary>
    public const double MinShare = 0.10;

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Slice(long Displayed, long Tokens, int Samples, double Seconds)
    {
        public double Factor => Tokens > 0 ? Displayed / (double)Tokens : double.PositiveInfinity;

        public bool IsActive => Samples >= FgWindow.MinPerBucket && Tokens > 0 && Factor >= FgWindow.ActiveThreshold;
    }

    /// <summary>The dominant active state over <paramref name="all"/> from <paramref name="start"/>, or null when none qualifies.</summary>
    public static FgSteadyState? From(IReadOnlyList<FrameSample> all, int start, long qpcFrequency)
    {
        ArgumentNullException.ThrowIfNull(all);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(qpcFrequency);
        if (start < 0 || start >= all.Count)
        {
            return null;
        }

        List<Slice> slices = Cut(all, start, qpcFrequency);
        double presenting = slices.Sum(static s => s.Seconds);
        List<Slice> active = [.. slices.Where(static s => s.IsActive)];
        if (active.Count == 0 || presenting <= 0)
        {
            return null;
        }

        // The state is the set of mutually agreeing windows holding the most time. Every active window is tried as
        // the centre; the winner is pooled and the set re-drawn once around the pooled factor, so the centre's own
        // noise does not decide membership.
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

        List<Slice> state = [.. Within(active, Pooled([.. Within(active, centre)]))];
        if (state.Count == 0)
        {
            return null;
        }

        long displayed = state.Sum(static s => s.Displayed);
        long tokens = state.Sum(static s => s.Tokens);
        double stateSeconds = state.Sum(static s => s.Seconds);
        double share = stateSeconds / presenting;
        return stateSeconds < MinSeconds || share < MinShare
            ? null
            : new FgSteadyState(displayed / (double)tokens, tokens / stateSeconds, displayed / stateSeconds, stateSeconds, share, state.Count);
    }

    private static IEnumerable<Slice> Within(List<Slice> active, double factor) =>
        active.Where(s => Math.Abs(s.Factor - factor) <= factor * StateTolerance);

    private static double Pooled(List<Slice> slices) =>
        slices.Sum(static s => s.Displayed) / (double)Math.Max(1, slices.Sum(static s => s.Tokens));

    /// <summary>
    /// Fixed wall-clock windows from the first claimed sample. A window's seconds are its nominal length, except the
    /// last, which ends at the last sample; a window with no sample in it (the title minimised) is not a window.
    /// </summary>
    private static List<Slice> Cut(IReadOnlyList<FrameSample> all, int start, long qpcFrequency)
    {
        ulong origin = all[start].Qpc;
        ulong last = all[^1].Qpc;
        double ticksPerWindow = WindowSeconds * qpcFrequency;
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

            double windowStart = origin + (index * ticksPerWindow);
            double seconds = Math.Min(WindowSeconds, Math.Max(0, (last - windowStart) / qpcFrequency));
            slices.Add(new Slice(displayed, tokens, samples, seconds > 0 ? seconds : WindowSeconds));
        }

        for (int i = start; i < all.Count; i++)
        {
            FrameSample s = all[i];
            long at = s.Qpc >= origin ? (long)((s.Qpc - origin) / ticksPerWindow) : 0;
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
