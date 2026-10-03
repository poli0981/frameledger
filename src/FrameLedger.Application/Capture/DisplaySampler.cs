// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Diagnostics;
using FrameLedger.Domain.Display;
using FrameLedger.Shared;

namespace FrameLedger.Application.Capture;

/// <summary>
/// The display mode, sampled on the capture loop's own ticks (beta.10, <c>03_METRICS</c> §Display mode): what the injected
/// component said about the swap chain (shared-memory region 4) and what the game's window looks like from outside
/// (<see cref="IDisplayProbe"/>), classified, and added up by the time each sample stood for.
/// </summary>
/// <remarks>
/// <para>
/// <b>A sample stands for the time until the next one.</b> Hooked, the loop ticks every 100 ms; held at Tier 2, every
/// second — so the tally is in milliseconds, not ticks, and a session's shares mean the same whichever loop ran it. The last
/// sample is closed by <see cref="Finish"/>.
/// </para>
/// <para>
/// <b>A pause is not time.</b> An interval that ends on a paused tick is dropped, the rule the frame series follows (a pause
/// is a gap, beta.8).
/// </para>
/// <para>
/// <b>A stale swap-chain sample says nothing about now.</b> The component samples only on a real present; a minimised game
/// issues only occlusion probes, which the hook drops. A region-4 sample older than <see cref="FreshFor"/> keeps its window
/// but loses its exclusive bits.
/// </para>
/// </remarks>
public sealed class DisplaySampler(IDisplayProbe? probe, Func<long>? clock = null)
{
    /// <summary>How long a region-4 sample says anything about the swap chain's exclusive state.</summary>
    public static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(2);

    private readonly DisplayTally _tally = new();
    private readonly Func<long> _clock = clock ?? Stopwatch.GetTimestamp;
    private DisplayObservation? _previous;
    private long _previousAt;

    /// <summary>Samples once; the sample before it is added for the time between the two.</summary>
    /// <param name="pids">The game's processes: the pinned one, or every process running its executable (Tier 2).</param>
    /// <param name="region">Region 4, when the loop is attached to a ring; null at Tier 2.</param>
    /// <param name="paused">Whether the session is paused at this tick.</param>
    public void Sample(IReadOnlyCollection<int> pids, FlDisplayState? region, bool paused)
    {
        ArgumentNullException.ThrowIfNull(pids);
        long now = _clock();
        Close(now, paused);
        if (probe is null)
        {
            return;
        }

        SwapChainView? chain = ViewOf(region, now);
        WindowView? window = probe.Observe(pids, chain?.Hwnd ?? 0);
        _previous = DisplayModeClassifier.Classify(chain, window);
        _previousAt = now;
    }

    /// <summary>Closes the last sample at the session's end.</summary>
    public void Finish(bool paused) => Close(_clock(), paused);

    /// <summary>The session's display facts, or null when nothing was ever sampled.</summary>
    public DisplaySummary? Summary() => _tally.Any ? _tally.Summary() : null;

    /// <summary>Region 4 as the classifier needs it, or null when the component described nothing.</summary>
    public static SwapChainView? ViewOf(FlDisplayState? region, long nowTimestamp)
    {
        if (region is not { } r || (r.Flags & FlDisplayFlags.Sampled) == FlDisplayFlags.None)
        {
            return null;
        }

        long at = unchecked((long)r.SampleQpc);
        bool fresh = nowTimestamp >= at && nowTimestamp - at < (long)(FreshFor.TotalSeconds * Stopwatch.Frequency);
        return new SwapChainView(
            Hwnd: r.Hwnd,
            ExclusiveKnown: (r.Flags & FlDisplayFlags.ExclusiveKnown) != FlDisplayFlags.None,
            Exclusive: (r.Flags & FlDisplayFlags.Exclusive) != FlDisplayFlags.None,
            OpenGl: (r.Flags & FlDisplayFlags.OpenGl) != FlDisplayFlags.None,
            BufferWidth: r.BufferWidth,
            BufferHeight: r.BufferHeight,
            SwapEffect: SwapEffectName(r.SwapEffect),
            Fresh: fresh);
    }

    /// <summary>
    /// <c>DXGI_SWAP_EFFECT</c> by name, from region 4's value-plus-one (0 = not read — the vendor's DISCARD is 0); an effect
    /// this build does not know is named by its number rather than guessed.
    /// </summary>
    public static string? SwapEffectName(uint plusOne) => plusOne switch
    {
        0 => null,
        1 => "discard",
        2 => "sequential",
        4 => "flip_sequential",
        5 => "flip_discard",
        _ => "effect_" + (plusOne - 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    private void Close(long now, bool paused)
    {
        if (_previous is { } previous && !paused)
        {
            _tally.Add(previous, (now - _previousAt) * 1000 / Stopwatch.Frequency);
        }

        _previous = null;
    }
}
