// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Display;

/// <summary>
/// A session's display samples, accumulated by the TIME each one stood for (beta.10): the capture loop samples every drain
/// tick (100 ms) while hooked and every hold tick (1 s) at Tier 2, and a sample counts for the wall time until the next —
/// so the two rates add up in one unit. Time the session was paused is not added (a pause is a gap, <c>03_METRICS</c>).
/// </summary>
public sealed class DisplayTally
{
    private readonly Dictionary<DisplayMode, long> _ms = [];
    private readonly Dictionary<DisplayMode, DisplayObservation> _last = [];
    private DisplayMode? _previous;
    private int _changes;
    private string? _source;
    private string? _swapEffect;

    /// <summary>Adds one sample that stood for <paramref name="elapsedMs"/> milliseconds (negative is treated as 0).</summary>
    public void Add(DisplayObservation observation, long elapsedMs)
    {
        long ms = Math.Max(0, elapsedMs);
        _ms[observation.Mode] = _ms.GetValueOrDefault(observation.Mode) + ms;
        _last[observation.Mode] = observation;
        if (DisplaySource.Strength(observation.Source) > DisplaySource.Strength(_source))
        {
            _source = observation.Source;
        }

        if (observation.SwapEffect is { } effect)
        {
            _swapEffect = effect;
        }

        if (observation.Mode != DisplayMode.NoWindow)
        {
            if (_previous is { } before && before != observation.Mode)
            {
                _changes++;
            }

            _previous = observation.Mode;
        }
    }

    /// <summary>Whether any sample was added at all (a session that never sampled stores no display facts).</summary>
    public bool Any => _ms.Count > 0;

    public DisplaySummary Summary()
    {
        var summary = new DisplaySummary
        {
            ExclusiveMs = _ms.GetValueOrDefault(DisplayMode.ExclusiveFullscreen),
            BorderlessMs = _ms.GetValueOrDefault(DisplayMode.Borderless),
            WindowedMs = _ms.GetValueOrDefault(DisplayMode.Windowed),
            CoversMs = _ms.GetValueOrDefault(DisplayMode.CoversScreen),
            MinimizedMs = _ms.GetValueOrDefault(DisplayMode.Minimized),
            NoWindowMs = _ms.GetValueOrDefault(DisplayMode.NoWindow),
            Changes = _changes,
            Source = _source,
            SwapEffect = _swapEffect,
        };

        if (summary.Dominant is not { } dominant || !_last.TryGetValue(dominant, out DisplayObservation seen))
        {
            return summary;
        }

        return summary with
        {
            WindowWidth = seen.Client?.Width,
            WindowHeight = seen.Client?.Height,
            BufferWidth = seen.BufferWidth,
            BufferHeight = seen.BufferHeight,
            MonitorWidth = seen.Monitor?.Width,
            MonitorHeight = seen.Monitor?.Height,
            MonitorHz = seen.MonitorHz,
        };
    }
}
