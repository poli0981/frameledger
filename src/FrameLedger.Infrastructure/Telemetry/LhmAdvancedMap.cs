// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Text;
using FrameLedger.Application.Advanced;
using LibreHardwareMonitor.Hardware;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// Which catalog family a LibreHardwareMonitor sensor is a member of, and what its series is called
/// (owner decision D101): the device at the top of the sensor's tree and the sensor's type decide the family, the
/// library's identifier names the member.
/// </summary>
/// <remarks>
/// <b>A sensor with no family is not read.</b> The table below is the whole of what is taken — a network adapter's
/// throughput, a drive's anything, a type the catalog has no entry for stay where they are. That is the difference
/// between "every sensor of the card" and "every sensor the library can find".
/// </remarks>
public static class LhmAdvancedMap
{
    /// <summary>The family's id for a sensor of <paramref name="type"/> under a device of <paramref name="root"/>, or null.</summary>
    public static string? FamilyOf(HardwareType root, SensorType type) => root switch
    {
        HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => type switch
        {
            SensorType.Temperature => "lhm.gpu.temperature",
            SensorType.Load => "lhm.gpu.load",
            SensorType.SmallData => "lhm.gpu.memory",
            SensorType.Clock => "lhm.gpu.clock",
            SensorType.Power => "lhm.gpu.power",
            SensorType.Fan => "lhm.gpu.fan",
            SensorType.Control => "lhm.gpu.fan_control",
            SensorType.Voltage => "lhm.gpu.voltage",
            SensorType.Current => "lhm.gpu.current",
            SensorType.Throughput => "lhm.gpu.throughput",
            _ => null,
        },
        HardwareType.Cpu => type switch
        {
            SensorType.Temperature => "lhm.cpu.temperature",
            SensorType.Load => "lhm.cpu.load",
            SensorType.Clock => "lhm.cpu.clock",
            SensorType.Power => "lhm.cpu.power",
            SensorType.Voltage => "lhm.cpu.voltage",
            _ => null,
        },
        HardwareType.Motherboard => type switch
        {
            SensorType.Temperature => "lhm.board.temperature",
            SensorType.Fan => "lhm.board.fan",
            SensorType.Control => "lhm.board.fan_control",
            SensorType.Voltage => "lhm.board.voltage",
            _ => null,
        },
        HardwareType.Memory => type switch
        {
            SensorType.Load => "lhm.memory.load",
            SensorType.Temperature => "lhm.memory.temperature",
            _ => null,
        },
        _ => null,
    };

    /// <summary>
    /// The instance key of a sensor: the library's identifier (<c>/lpc/nct6798d/0/voltage/3</c>) without the segment
    /// that repeats the sensor's type, in lower case, with every other character an underscore
    /// (<c>lpc_nct6798d_0_3</c>). Null when nothing usable is left.
    /// </summary>
    /// <remarks>
    /// The identifier is the library's own stable name for a sensor on a PC — a path of device kind, index and sensor
    /// index. It holds no serial number: the library builds it from positions, not from what a device reports.
    /// </remarks>
    public static string? KeyOf(string? identifier, SensorType type)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        string typeSegment = type.ToString();
        var key = new StringBuilder(identifier.Length);
        foreach (string segment in identifier.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(segment, typeSegment, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (key.Length > 0)
            {
                key.Append('_');
            }

            foreach (char c in segment)
            {
                char lower = char.ToLowerInvariant(c);
                key.Append(lower is (>= 'a' and <= 'z') or (>= '0' and <= '9') ? lower : '_');
            }
        }

        // The tail carries the indices that tell two sensors apart, so a key too long keeps its end.
        string text = key.Length > AdvancedMetricCatalog.MaxInstanceKeyLength
            ? key.ToString(key.Length - AdvancedMetricCatalog.MaxInstanceKeyLength, AdvancedMetricCatalog.MaxInstanceKeyLength)
            : key.ToString();
        text = text.Trim('_');
        return AdvancedMetricCatalog.IsInstanceKey(text) ? text : null;
    }

    /// <summary>What the hardware calls a sensor, for the page: the device's name, then the sensor's.</summary>
    public static string LabelOf(ISensor sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);
        string device = sensor.Hardware?.Name?.Trim() ?? string.Empty;
        string name = sensor.Name?.Trim() ?? string.Empty;
        return (device.Length, name.Length) switch
        {
            (0, 0) => sensor.Identifier?.ToString() ?? string.Empty,
            (0, _) => name,
            (_, 0) => device,
            _ => $"{device} - {name}",
        };
    }
}
