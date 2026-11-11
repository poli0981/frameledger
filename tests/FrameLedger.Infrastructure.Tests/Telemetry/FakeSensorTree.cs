// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Infrastructure.Telemetry;
using LibreHardwareMonitor.Hardware;
using NSubstitute;

namespace FrameLedger.Infrastructure.Tests.Telemetry;

/// <summary>
/// A LibreHardwareMonitor tree made of substitutes, and an <see cref="ILhmComputer"/> over it, so the advanced
/// readers are tested with no sensor in the room — which is what CI is.
/// </summary>
internal static class FakeSensorTree
{
    public static IHardware Device(HardwareType type, string name, params ISensor[] sensors)
    {
        IHardware h = Substitute.For<IHardware>();
        h.HardwareType.Returns(type);
        h.Name.Returns(name);
        h.Sensors.Returns(sensors);
        h.SubHardware.Returns([]);
        h.Parent.Returns((IHardware?)null);
        foreach (ISensor s in sensors)
        {
            s.Hardware.Returns(h);
        }

        return h;
    }

    public static IHardware WithSub(this IHardware parent, params IHardware[] subs)
    {
        parent.SubHardware.Returns(subs);
        foreach (IHardware sub in subs)
        {
            sub.Parent.Returns(parent);
        }

        return parent;
    }

    /// <summary>A sensor whose identifier is <paramref name="path"/>, e.g. <c>/gpu-nvidia/0/temperature/0</c>.</summary>
    public static ISensor Sensor(SensorType type, string path, string name, float? value)
    {
        ISensor s = Substitute.For<ISensor>();
        s.SensorType.Returns(type);
        s.Name.Returns(name);
        s.Value.Returns(value);
        s.Identifier.Returns(new Identifier(path.Split('/', StringSplitOptions.RemoveEmptyEntries)));
        return s;
    }

    /// <summary>A graphics card, a processor, a motherboard with its sensor chip, and a memory device.</summary>
    public static IHardware[] WholePc()
    {
        IHardware gpu = Device(HardwareType.GpuNvidia, "NVIDIA GeForce RTX 5080",
            Sensor(SensorType.Temperature, "/gpu-nvidia/0/temperature/0", "GPU Core", 61f),
            Sensor(SensorType.Control, "/gpu-nvidia/0/control/1", "GPU Fan 1", 42f),
            Sensor(SensorType.Voltage, "/gpu-nvidia/0/voltage/0", "GPU Core", 1.05f),
            Sensor(SensorType.Throughput, "/gpu-nvidia/0/throughput/0", "GPU PCIe Rx", 2048f));
        IHardware cpu = Device(HardwareType.Cpu, "Intel Core i7-14700K",
            Sensor(SensorType.Temperature, "/intelcpu/0/temperature/0", "CPU Package", 71f),
            Sensor(SensorType.Temperature, "/intelcpu/0/temperature/3", "CPU Core #3", 68f),
            Sensor(SensorType.Load, "/intelcpu/0/load/0", "CPU Total", 37f),
            Sensor(SensorType.Power, "/intelcpu/0/power/0", "CPU Package", 96.5f));
        IHardware chip = Device(HardwareType.SuperIO, "Nuvoton NCT6798D",
            Sensor(SensorType.Voltage, "/lpc/nct6798d/0/voltage/7", "-12V", -11.97f),
            Sensor(SensorType.Fan, "/lpc/nct6798d/0/fan/1", "Fan #2", 940f),
            Sensor(SensorType.Temperature, "/lpc/nct6798d/0/temperature/2", "Temperature #3", 38f));
        IHardware board = Device(HardwareType.Motherboard, "ASUS ROG STRIX Z790-E").WithSub(chip);
        IHardware memory = Device(HardwareType.Memory, "Total Memory",
            Sensor(SensorType.Load, "/ram/load/0", "Memory", 44f),
            Sensor(SensorType.Data, "/ram/data/0", "Memory Used", 14.1f));
        return [gpu, cpu, board, memory];
    }
}
