// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FrameLedger.Application.Input;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Recording;
using FrameLedger.Domain.Input;
using FrameLedger.Infrastructure.Blobs;

namespace FrameLedger.App.Charts;

/// <summary>
/// A session's input series decoded (0.1.0-beta.50, <c>06_DATA_MODEL</c> §sensor_blobs): what each tick was mostly
/// played with and each controller's activity and battery, on the sensors' clock. Unlike the advanced series a gap is
/// kept - NaN is "not connected" for a controller and "not in front" for the class, and where one starts and ends is
/// exactly what the marks are made of.
/// </summary>
/// <param name="TimesS">Seconds from the session's start, a value a tick.</param>
/// <param name="Class"><see cref="InputClass"/>'s number on each tick; NaN with no reading or with the game not in front.</param>
/// <param name="Devices">The controllers that have series, by their number.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "a data carrier for the charts, as SensorSeries is")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture, which changes at runtime")]
public sealed record InputSeries(double[] TimesS, float[] Class, IReadOnlyList<InputDeviceSeries> Devices)
{
    /// <summary>The most marks one chart draws: a connection that flaps would otherwise bury the chart under its own lines. The table has the counts.</summary>
    public const int MaxEvents = 60;

    /// <summary>The blobs of <c>FindInputSensorsAsync</c> decoded; null when the session stored no input series.</summary>
    public static InputSeries? Decode(IReadOnlyList<SensorBlob> blobs)
    {
        ArgumentNullException.ThrowIfNull(blobs);
        float[]? @class = Values(blobs, InputSessionBuilder.ClassSeries);
        if (@class is null)
        {
            return null;
        }

        float[]? tMs = Values(blobs, "t_ms");
        double hz = blobs.First(static b => string.Equals(b.Series, InputSessionBuilder.ClassSeries, StringComparison.Ordinal)).Hz;
        double[] times = new double[@class.Length];
        for (int i = 0; i < times.Length; i++)
        {
            times[i] = tMs is not null && i < tMs.Length ? tMs[i] / 1000.0 : i / Math.Max(hz, 0.001);
        }

        var devices = new List<InputDeviceSeries>();
        for (int index = 1; index <= InputSessionBuilder.MaxDevices; index++)
        {
            if (Values(blobs, InputSessionBuilder.ActiveSeries(index)) is { } active && active.Length == @class.Length)
            {
                float[]? battery = Values(blobs, InputSessionBuilder.BatterySeries(index));
                devices.Add(new InputDeviceSeries(index, active, battery?.Length == @class.Length ? battery : null));
            }
        }

        return new InputSeries(times, @class, devices);
    }

    /// <summary>
    /// The marks: each controller's connections and drops after the session's first reading, and each fall of a battery to
    /// low - in time order, <see cref="MaxEvents"/> at most. A controller that was there from the start did not
    /// "connect": where a series has one rising edge more than the row counted connects, the first is the start.
    /// </summary>
    public IReadOnlyList<InputEvent> Events(InputDevicesRecord? record)
    {
        var events = new List<InputEvent>();
        foreach (InputDeviceSeries device in Devices)
        {
            InputDeviceRecord? stored = record?.Devices.FirstOrDefault(d => d.Index == device.Index);
            AddConnections(events, device, stored?.Connects);
            if (device.Battery is { } battery && stored?.Battery is { } facts)
            {
                AddBatteryLow(events, device.Index, battery, InputNames.ScaleOf(facts.Scale));
            }
        }

        return [.. events.OrderBy(static e => e.TimeS).Take(MaxEvents)];
    }

    /// <summary>A kind of mark in a few words - a chart's legend entry.</summary>
    public static string Describe(InputEventKind kind) => kind switch
    {
        InputEventKind.Connected => Strings.Chart_Input_Event_Connected,
        InputEventKind.Disconnected => Strings.Chart_Input_Event_Disconnected,
        _ => Strings.Chart_Input_Event_BatteryLow,
    };

    private static float[]? Values(IReadOnlyList<SensorBlob> blobs, string series)
    {
        SensorBlob? blob = blobs.FirstOrDefault(b => string.Equals(b.Series, series, StringComparison.Ordinal));
        return blob is null || !string.Equals(blob.Codec, SeriesCodec.Tag, StringComparison.Ordinal) ? null : SeriesCodec.DecodeFloat32(blob.Data.ToArray());
    }

    private void AddConnections(List<InputEvent> events, InputDeviceSeries device, int? countedConnects)
    {
        var rising = new List<int>();
        bool connected = false;
        for (int i = 0; i < device.Active.Length; i++)
        {
            bool now = !float.IsNaN(device.Active[i]);
            if (now && !connected)
            {
                rising.Add(i);
            }
            else if (!now && connected)
            {
                events.Add(new InputEvent(TimesS[i], InputEventKind.Disconnected, device.Index));
            }

            connected = now;
        }

        // The row's count is the builder's, taken after the session's first reading: one edge more is the controller
        // that was already there. With no stored count, an edge on the very first tick is read the same way.
        int skip = countedConnects is { } counted ? Math.Max(0, rising.Count - counted) : rising is [0, ..] ? 1 : 0;
        events.AddRange(rising.Skip(skip).Select(i => new InputEvent(TimesS[i], InputEventKind.Connected, device.Index)));
    }

    private void AddBatteryLow(List<InputEvent> events, int index, float[] battery, BatteryScale scale)
    {
        // A falling level is a battery running down: the state is not in the series, and a level that falls is not charging.
        float? previous = null;
        for (int i = 0; i < battery.Length; i++)
        {
            if (float.IsNaN(battery[i]))
            {
                continue;
            }

            if (previous is { } before && !IsLow(before, scale) && IsLow(battery[i], scale))
            {
                events.Add(new InputEvent(TimesS[i], InputEventKind.BatteryLow, index));
            }

            previous = battery[i];
        }
    }

    private static bool IsLow(float level, BatteryScale scale) =>
        new BatteryReading(BatteryState.Discharging, scale, (int)Math.Round(level, MidpointRounding.AwayFromZero)).IsLow;

    /// <summary>The battery series as a percentage to draw: a percentage as it is, XInput's four levels as thirds (empty 0, low 33, medium 67, full 100).</summary>
    public static double ToPercent(float level, BatteryScale scale) => scale == BatteryScale.FourLevels
        ? Math.Round(Math.Clamp(level, 0f, 3f) * 100.0 / 3.0, MidpointRounding.AwayFromZero)
        : Math.Clamp(level, 0f, 100f);

    /// <summary>The label of a controller's line: its number, as the table's order has it.</summary>
    public static string DeviceLabel(int index) => string.Format(CultureInfo.CurrentCulture, Strings.Chart_Input_Device_Format, index);
}
