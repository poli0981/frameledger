// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Domain.Input;

/// <summary>
/// One controller's battery readings over a session, accumulated by the time each stood for (<c>03_METRICS</c> §Input):
/// how long it spent at each level, which lasted longest, where it started and ended, and how fast it ran down.
/// </summary>
public sealed class BatteryTally
{
    /// <summary>The least time on battery a rate is worked out over: a level moves in steps of ten, and less says nothing.</summary>
    public static readonly TimeSpan MinimumDrainTime = TimeSpan.FromMinutes(10);

    private readonly Dictionary<BatteryReading, long> _ms = [];
    private BatteryReading? _first;
    private BatteryReading? _last;
    private int? _drainFrom;
    private int _drainTo;
    private long _drainMs;
    private bool _drainBroken;

    /// <summary>Whether any reading was added.</summary>
    public bool Any => _first is not null;

    /// <summary>Adds one reading that stood for <paramref name="elapsedMs"/> milliseconds (negative is treated as 0).</summary>
    public void Add(BatteryReading reading, long elapsedMs)
    {
        long ms = Math.Max(0, elapsedMs);
        _ms[reading] = _ms.GetValueOrDefault(reading) + ms;
        _first ??= reading;
        _last = reading;
        if (reading.Scale != BatteryScale.Percent)
        {
            return;
        }

        if (reading.State == BatteryState.Discharging)
        {
            _drainFrom ??= reading.Level;
            _drainTo = reading.Level;
            _drainMs += ms;
        }
        else if (_drainFrom is not null && reading.State is BatteryState.Charging or BatteryState.Full)
        {
            // It was put on power after it had begun to run down: the fall between first and last is not one run.
            _drainBroken = true;
        }
    }

    public BatterySummary Summary()
    {
        BatteryStateTime[] states = [.. _ms
            .Select(static p => new BatteryStateTime(p.Key.State, p.Key.Scale, p.Key.Level, p.Value))
            .OrderByDescending(static s => s.Ms)
            .ThenBy(static s => s.State)
            .ThenByDescending(static s => s.Level)];
        return new BatterySummary
        {
            States = states,
            First = _first,
            Last = _last,
            DrainPercentPerHour = Drain(),
        };
    }

    private double? Drain()
    {
        if (_drainBroken || _drainFrom is not { } from || from <= _drainTo || _drainMs < MinimumDrainTime.TotalMilliseconds)
        {
            return null;
        }

        return Math.Round((from - _drainTo) / (_drainMs / 3_600_000d), 1);
    }
}
