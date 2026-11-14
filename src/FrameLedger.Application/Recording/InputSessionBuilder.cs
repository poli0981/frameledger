// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FrameLedger.Application.Input;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Telemetry;
using FrameLedger.Domain.Input;

namespace FrameLedger.Application.Recording;

/// <summary>
/// What a session stores of its input readings (<c>03_METRICS</c> §Input, owner decisions of 2026-11-14): the totals,
/// the controllers with their batteries and makers, and the series a timeline is drawn from - all from the samples, so
/// a session recovered from its <c>.partial</c> file has them as a finished one does.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing in, nothing out.</b> A session none of whose samples carries a reading - the option was off, or the
/// terms that say what is read were not accepted yet - stores no series and no summary, and is byte for byte what it
/// was before this existed.
/// </para>
/// <para>
/// <b>One pad seen twice is one controller.</b> Windows shows a controller that Steam Input stands in front of twice:
/// the one the player holds, and an Xbox pad Steam presents for it. An XInput pad that was in use at the same moments
/// as another controller is folded into it (<see cref="VirtualPad"/>) - into a controller that is not XInput's at any
/// time, into another of XInput's pads only in a session where Steam's virtual gamepad was seen.
/// </para>
/// </remarks>
public static class InputSessionBuilder
{
    /// <summary>The most controllers one session stores series for; more are counted in the totals and not listed.</summary>
    public const int MaxDevices = 8;

    public const string ClassSeries = "input.class";

    /// <summary>What every series of this builder starts with: how a reader tells them from the sensors' and the advanced ones.</summary>
    public const string SeriesPrefix = "input.";

    public static string ActiveSeries(int index) => string.Create(CultureInfo.InvariantCulture, $"input.dev.{index}.active");

    public static string BatterySeries(int index) => string.Create(CultureInfo.InvariantCulture, $"input.dev.{index}.battery");

    /// <summary>The session's input series as blobs and its summary - nothing when no sample carries a reading.</summary>
    /// <param name="sensors">The session's samples, in order.</param>
    /// <param name="codec">How a series is encoded.</param>
    /// <param name="hz">The rate the samples had, stored with each series as the fallback for a reader with no <c>t_ms</c>.</param>
    public static (IReadOnlyList<SensorBlob> Blobs, InputSessionSummary? Summary) Build(IReadOnlyList<TelemetrySample> sensors, ISeriesCodec codec, double hz = 1)
    {
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(codec);
        if (!sensors.Any(static s => s.Input is not null))
        {
            return ([], null);
        }

        List<Track> tracks = Tracks(sensors);
        bool virtualPad = sensors.Any(static s => s.Input?.VirtualPad == true);
        Fold(tracks, virtualPad);
        Track[] kept = [.. tracks.Where(static t => !t.Folded)];

        var blobs = new List<SensorBlob> { Blob(ClassSeries, ClassColumn(sensors), codec, hz) };
        var devices = new List<InputDeviceRecord>(kept.Length);
        for (int d = 0; d < kept.Length; d++)
        {
            devices.Add(kept[d].ToRecord(d + 1));
            if (d < MaxDevices)
            {
                blobs.Add(Blob(ActiveSeries(d + 1), kept[d].ActiveColumn(sensors), codec, hz));
                if (kept[d].Battery.Any(static b => b is { Scale: not BatteryScale.None }))
                {
                    blobs.Add(Blob(BatterySeries(d + 1), kept[d].BatteryColumn(), codec, hz));
                }
            }
        }

        InputHealth health = sensors.Last(static s => s.Input is not null).Input!.Health;
        var record = new InputDevicesRecord
        {
            Devices = devices,
            Families = Families(kept),
            FamilySwitches = InputSwitches.Count(FamilyTicks(kept, sensors.Count)),
            VirtualPad = virtualPad,
            Health = health,
        };

        return (blobs, new InputSessionSummary
        {
            ObservedMs = sensors.Sum(static s => (long)(s.Input?.ObservedMs ?? 0)),
            ForegroundMs = sensors.Sum(static s => (long)(s.Input?.ForegroundMs ?? 0)),
            ControllerMs = sensors.Sum(static s => (long)(s.Input?.ControllerMs ?? 0)),
            KeyboardMouseMs = sensors.Sum(static s => (long)(s.Input?.KeyboardMouseMs ?? 0)),
            BothMs = sensors.Sum(static s => (long)(s.Input?.BothMs ?? 0)),
            IdleMs = sensors.Sum(static s => (long)(s.Input?.IdleMs ?? 0)),
            MethodSwitches = InputSwitches.Count(sensors.Select(static s => s.Input is { } r
                ? InputSwitches.Exclusive(r.ControllerMs, r.KeyboardMouseMs, r.BothMs)
                : null)),
            Source = health.Sources(),
            DevicesJson = InputDevicesRecord.ToJson(record),
        });
    }

    private static SensorBlob Blob(string series, float[] column, ISeriesCodec codec, double hz) =>
        new() { Series = series, Hz = hz, Codec = codec.Tag, Data = codec.EncodeSparseFloat32(column) };

    /// <summary>What each tick was mostly played with, as <see cref="InputClass"/>'s number; NaN for a tick with no reading or with the game not in front.</summary>
    private static float[] ClassColumn(IReadOnlyList<TelemetrySample> sensors)
    {
        float[] column = new float[sensors.Count];
        for (int i = 0; i < sensors.Count; i++)
        {
            column[i] = sensors[i].Input is { } r && InputSwitches.Dominant(r.ControllerMs, r.KeyboardMouseMs, r.BothMs, r.IdleMs) is { } dominant
                ? (int)dominant
                : float.NaN;
        }

        return column;
    }

    /// <summary>One track per controller the samples name, in the order they first appear.</summary>
    private static List<Track> Tracks(IReadOnlyList<TelemetrySample> sensors)
    {
        var tracks = new List<Track>();
        var byId = new Dictionary<int, Track>();
        for (int i = 0; i < sensors.Count; i++)
        {
            if (sensors[i].Input is not { } reading)
            {
                continue;
            }

            foreach (InputDevice device in reading.Table)
            {
                if (!byId.TryGetValue(device.Id, out Track? track))
                {
                    track = new Track(device, sensors.Count);
                    byId[device.Id] = track;
                    tracks.Add(track);
                }

                track.Device = device;
            }

            foreach (InputDeviceReading d in reading.Devices)
            {
                if (byId.TryGetValue(d.DeviceId, out Track? track))
                {
                    track.Add(i, d, reading.SpanMs);
                }
            }
        }

        return tracks;
    }

    /// <summary>Folds each of XInput's pads that moved with another controller into that controller.</summary>
    private static void Fold(List<Track> tracks, bool virtualPad)
    {
        foreach (Track pad in tracks.Where(static t => t.Device.XInputSlot is not null))
        {
            // A controller that is not XInput's first: it is the one the player holds. Then, only with Steam's pad seen,
            // an XInput pad of a lower number - which of the two Steam presents cannot be told, and it does not matter.
            Track? into = tracks.FirstOrDefault(t => !t.Folded && t.Device.XInputSlot is null && VirtualPad.IsSamePad(pad.ActiveMs, t.ActiveMs));
            if (into is null && virtualPad)
            {
                into = tracks.FirstOrDefault(t => !t.Folded && !ReferenceEquals(t, pad) && t.Device.XInputSlot < pad.Device.XInputSlot
                    && VirtualPad.IsSamePad(pad.ActiveMs, t.ActiveMs));
            }

            if (into is not null)
            {
                pad.Folded = true;
                into.Remapped = true;
            }
        }
    }

    private static List<InputFamilyRecord> Families(Track[] kept)
    {
        long total = kept.Sum(static t => t.TotalActiveMs);
        return [.. kept
            .GroupBy(static t => t.Device.Family)
            .Select(g => (Family: g.Key, Ms: g.Sum(static t => t.TotalActiveMs)))
            .Where(static f => f.Ms > 0)
            .OrderByDescending(static f => f.Ms)
            .ThenBy(static f => f.Family)
            .Select(f => new InputFamilyRecord(InputNames.Of(f.Family), f.Ms, Math.Round((double)f.Ms / total, 4)))];
    }

    /// <summary>The one maker whose controller was in use on each tick, or null where none or more than one was.</summary>
    private static IEnumerable<ControllerFamily?> FamilyTicks(Track[] kept, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            ControllerFamily? only = null;
            bool several = false;
            foreach (Track track in kept)
            {
                if (track.ActiveMs[i] <= 0)
                {
                    continue;
                }

                several |= only is { } seen && seen != track.Device.Family;
                only = track.Device.Family;
            }

            yield return several ? null : only;
        }
    }

    /// <summary>One controller across the session's ticks.</summary>
    private sealed class Track(InputDevice device, int ticks)
    {
        private readonly bool[] _connected = new bool[ticks];
        private readonly BatteryTally _tally = new();
        private long _connectedMs;
        private int _connects;
        private int _disconnects;

        public InputDevice Device { get; set; } = device;

        public int[] ActiveMs { get; } = new int[ticks];

        public BatteryReading?[] Battery { get; } = new BatteryReading?[ticks];

        public long TotalActiveMs => ActiveMs.Sum(static ms => (long)ms);

        /// <summary>Another controller's, and not listed.</summary>
        public bool Folded { get; set; }

        /// <summary>A pad that moved with it was folded into it.</summary>
        public bool Remapped { get; set; }

        public void Add(int tick, InputDeviceReading reading, int spanMs)
        {
            ActiveMs[tick] = reading.ActiveMs;
            _connected[tick] = reading.Connected;
            _connects += reading.Connects;
            _disconnects += reading.Disconnects;
            if (!reading.Connected)
            {
                return;
            }

            _connectedMs += spanMs;
            if (reading.Battery is { } battery)
            {
                Battery[tick] = battery;
                _tally.Add(battery, spanMs);
            }
        }

        /// <summary>The part of each tick's time in front that the controller was in use, 0 to 1; NaN where it was not connected.</summary>
        public float[] ActiveColumn(IReadOnlyList<TelemetrySample> sensors)
        {
            float[] column = new float[ActiveMs.Length];
            for (int i = 0; i < column.Length; i++)
            {
                int front = sensors[i].Input?.ForegroundMs ?? 0;
                column[i] = !_connected[i] ? float.NaN : front <= 0 ? 0f : Math.Clamp((float)ActiveMs[i] / front, 0f, 1f);
            }

            return column;
        }

        /// <summary>The battery's level on each tick, in its own scale; NaN where there was none.</summary>
        public float[] BatteryColumn()
        {
            float[] column = new float[Battery.Length];
            for (int i = 0; i < column.Length; i++)
            {
                column[i] = Battery[i] is { Scale: not BatteryScale.None } b ? b.Level : float.NaN;
            }

            return column;
        }

        public InputDeviceRecord ToRecord(int index) => new()
        {
            Index = index,
            Family = InputNames.Of(Device.Family),
            VendorId = Device.VendorId,
            ProductId = Device.ProductId,
            Model = Device.Model,
            Connection = InputNames.Of(Device.Connection),
            XInputSlot = Device.XInputSlot,
            Virtual = Device.IsVirtual,
            Remapped = Remapped,
            ConnectedMs = _connectedMs,
            ActiveMs = TotalActiveMs,
            Connects = _connects,
            Disconnects = _disconnects,
            Battery = BatteryRecord(),
        };

        private InputBatteryRecord? BatteryRecord()
        {
            if (!_tally.Any)
            {
                return null;
            }

            BatterySummary summary = _tally.Summary();
            BatteryScale scale = summary.States.Select(static s => s.Scale).FirstOrDefault(static s => s != BatteryScale.None);
            return new InputBatteryRecord
            {
                Scale = InputNames.Of(scale),
                States = [.. summary.States.Select(static s => new InputBatteryStateRecord(InputNames.Of(s.State), s.Level, s.Ms))],
                First = summary.First is { Scale: not BatteryScale.None } first ? first.Level : null,
                Last = summary.Last is { Scale: not BatteryScale.None } last ? last.Level : null,
                DrainPercentPerHour = summary.DrainPercentPerHour,
            };
        }
    }
}
