// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Domain.Input;

namespace FrameLedger.Application.Input;

/// <summary>
/// One session's slices added up into one <see cref="InputReading"/> per tick (<c>03_METRICS</c> §Input). The reader
/// adds a slice about ten times a second on its own thread; the telemetry poller takes the sum once a second on its.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only time the game was in front is classified.</b> A slice in which another program's window was in front counts
/// for nothing but <see cref="InputReading.SpanMs"/>: typing in a browser beside the game is not the game played with
/// the keyboard. A slice of a paused session is not observed either (a pause is a gap, <c>03_METRICS</c>).
/// </para>
/// <para>
/// <b>A controller that is there when the session starts has not connected.</b> Connections and disconnections are
/// counted from the second slice on, by comparing each slice's controllers with those of the slice before - paused or
/// not, in front or not: a pad that drops out while the player is in another window still dropped out.
/// </para>
/// </remarks>
public sealed class InputTickAccumulator
{
    private readonly Lock _gate = new();
    private readonly Dictionary<int, Entry> _devices = [];
    private HashSet<int> _connected = [];
    private bool _started;
    private InputHealth? _healthAtStart;
    private InputHealth _health = new();
    private int _spanMs;
    private int _observedMs;
    private int _foregroundMs;
    private int _controllerMs;
    private int _keyboardMouseMs;
    private int _bothMs;
    private int _idleMs;
    private bool _virtualPad;
    private bool _any;

    /// <summary>Adds one slice.</summary>
    /// <param name="slice">What the reader saw.</param>
    /// <param name="inFront">Whether the process in front is one of the session's.</param>
    /// <param name="paused">Whether the session is paused.</param>
    public void Add(InputSlice slice, bool inFront, bool paused)
    {
        ArgumentNullException.ThrowIfNull(slice);
        int elapsed = Math.Max(0, slice.ElapsedMs);
        lock (_gate)
        {
            _any = true;
            _healthAtStart ??= slice.Health;
            _health = slice.Health;
            _spanMs += elapsed;
            _virtualPad |= slice.VirtualPad;
            bool counted = inFront && !paused;
            bool controller = Track(slice.Devices, counted ? elapsed : 0);
            if (paused)
            {
                return;
            }

            _observedMs += elapsed;
            if (!inFront)
            {
                return;
            }

            _foregroundMs += elapsed;
            switch (controller, slice.KeyboardMouseActive)
            {
                case (true, true):
                    _bothMs += elapsed;
                    break;
                case (true, false):
                    _controllerMs += elapsed;
                    break;
                case (false, true):
                    _keyboardMouseMs += elapsed;
                    break;
                default:
                    _idleMs += elapsed;
                    break;
            }
        }
    }

    /// <summary>The sum since the last take, or null when no slice was added since then.</summary>
    public InputReading? Take()
    {
        lock (_gate)
        {
            if (!_any)
            {
                return null;
            }

            var devices = new List<InputDeviceReading>(_devices.Count);
            var table = new List<InputDevice>(_devices.Count);
            foreach ((int id, Entry entry) in _devices.OrderBy(static p => p.Key))
            {
                devices.Add(new InputDeviceReading(id, _connected.Contains(id), entry.ActiveMs, entry.Battery, entry.Connects, entry.Disconnects));
                table.Add(entry.Device);
            }

            var reading = new InputReading
            {
                SpanMs = _spanMs,
                ObservedMs = _observedMs,
                ForegroundMs = _foregroundMs,
                ControllerMs = _controllerMs,
                KeyboardMouseMs = _keyboardMouseMs,
                BothMs = _bothMs,
                IdleMs = _idleMs,
                VirtualPad = _virtualPad,
                Devices = devices,
                Table = table,
                Health = _health.Since(_healthAtStart ?? _health),
            };

            // The next tick starts with the controllers still connected and nothing counted for them yet.
            foreach (int id in _devices.Keys.Where(id => !_connected.Contains(id)).ToArray())
            {
                _devices.Remove(id);
            }

            foreach (Entry entry in _devices.Values)
            {
                entry.Reset();
            }

            _spanMs = _observedMs = _foregroundMs = _controllerMs = _keyboardMouseMs = _bothMs = _idleMs = 0;
            _virtualPad = false;
            _any = false;
            return reading;
        }
    }

    /// <summary>Notes each controller of the slice and what changed since the slice before; returns whether any was in use.</summary>
    private bool Track(IReadOnlyList<InputSliceDevice> devices, int countedMs)
    {
        var now = new HashSet<int>(devices.Count);
        bool anyActive = false;
        foreach (InputSliceDevice device in devices)
        {
            int id = device.Device.Id;
            if (!now.Add(id))
            {
                continue;
            }

            if (!_devices.TryGetValue(id, out Entry? entry))
            {
                entry = new Entry(device.Device);
                _devices[id] = entry;
            }

            entry.Device = device.Device;
            entry.Battery = device.Battery ?? entry.Battery;
            if (_started && !_connected.Contains(id))
            {
                entry.Connects++;
            }

            if (device.Active)
            {
                anyActive = true;
                entry.ActiveMs += countedMs;
            }
        }

        foreach (int gone in _connected)
        {
            if (!now.Contains(gone) && _devices.TryGetValue(gone, out Entry? entry))
            {
                entry.Disconnects++;
            }
        }

        _connected = now;
        _started = true;
        return anyActive;
    }

    private sealed class Entry(InputDevice device)
    {
        public InputDevice Device { get; set; } = device;

        public int ActiveMs { get; set; }

        public BatteryReading? Battery { get; set; }

        public int Connects { get; set; }

        public int Disconnects { get; set; }

        public void Reset()
        {
            ActiveMs = 0;
            Connects = 0;
            Disconnects = 0;
        }
    }
}
