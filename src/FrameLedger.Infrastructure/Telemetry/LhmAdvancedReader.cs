// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Advanced;
using LibreHardwareMonitor.Hardware;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// LibreHardwareMonitor's part of "Advanced metrics" (owner decisions D101 and D103): every sensor of the graphics
/// card, and — where the Agent runs as administrator with PawnIO — of the processor, the memory and the motherboard,
/// each as a member of a catalog family.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own <c>Computer</c>.</b> Not the telemetry layer's (L2), which is GPU-only, per session and unelevated by
/// design, and not the one <see cref="LhmCpuTemperatureReader"/> opens: this one lives as long as the shared reader
/// does. What may be opened is <see cref="GroupsFor"/>'s answer and nothing else.
/// </para>
/// <para>
/// <b>A device that throws is skipped, not retried in a loop.</b> The library's own traversal stops at the first
/// exception, so one faulting sensor chip would silence every device after it. Each top-level device is refreshed in
/// a guard of its own; two faults and it is left alone until the reader is opened again — its series simply stop
/// having readings (<c>18_GPU_VENDOR_APIS</c> §Runtime policy).
/// </para>
/// <para>
/// <b>Not every device every tick.</b> Memory modules are read over SMBus and change slowly: every fifth tick.
/// </para>
/// </remarks>
public sealed class LhmAdvancedReader : IDisposable
{
    /// <summary>Faults after which a device is left alone.</summary>
    public const int MaxFaults = 2;

    /// <summary>How many ticks pass between two refreshes of the memory group.</summary>
    public const int MemoryEveryTicks = 5;

    private readonly ILhmComputer _computer;
    private readonly Dictionary<IHardware, int> _faults = new(ReferenceEqualityComparer.Instance);
    private IReadOnlyList<IHardware> _hardware = [];
    private long _tick;
    private bool _open;
    private bool _disposed;

    public LhmAdvancedReader(ILhmComputer computer)
    {
        ArgumentNullException.ThrowIfNull(computer);
        _computer = computer;
    }

    /// <summary>
    /// The groups this process may open: the graphics cards always; the processor, memory and motherboard only when
    /// it is elevated AND PawnIO is installed (D103). An unknown answer about PawnIO is not a yes.
    /// </summary>
    public static LhmGroups GroupsFor(bool elevated, bool? pawnIoInstalled)
        => elevated && pawnIoInstalled == true
            ? LhmGroups.Gpu | LhmGroups.Cpu | LhmGroups.Memory | LhmGroups.Motherboard
            : LhmGroups.Gpu;

    /// <summary>Devices left alone after <see cref="MaxFaults"/> faults.</summary>
    public int SkippedDevices => _faults.Count(static f => f.Value >= MaxFaults);

    /// <summary>What the last fault said, for the log; null when there was none.</summary>
    public string? LastFault { get; private set; }

    /// <summary>Opens the groups. False when the library threw: nothing is read, and <see cref="LastFault"/> says why.</summary>
    public bool Open()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_open)
        {
            return true;
        }

        try
        {
            _computer.Open();
            _hardware = _computer.Hardware;
            _open = true;
            return true;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LastFault = $"open: {e.GetType().Name}: {e.Message}";
            return false;
        }
    }

    /// <summary>Refreshes every device that is due on this tick and has not faulted out.</summary>
    public void Refresh()
    {
        if (!_open || _disposed)
        {
            return;
        }

        long tick = _tick++;
        foreach (IHardware device in _hardware)
        {
            if (_faults.GetValueOrDefault(device) >= MaxFaults || (device.HardwareType == HardwareType.Memory && tick % MemoryEveryTicks != 0))
            {
                continue;
            }

            try
            {
                Update(device, depth: 0);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _faults[device] = _faults.GetValueOrDefault(device) + 1;
                LastFault = $"{device.HardwareType} '{device.Name}': {e.GetType().Name}: {e.Message}";
            }
        }
    }

    /// <summary>
    /// The value every mapped sensor holds after the last <see cref="Refresh"/>, with the directory entry of each: a
    /// sensor with no value, or of a device that faulted out, has no reading.
    /// </summary>
    public (IReadOnlyList<AdvancedReading> Readings, IReadOnlyList<AdvancedSeriesInfo> Series) Read()
    {
        var readings = new List<AdvancedReading>();
        var series = new List<AdvancedSeriesInfo>();
        if (!_open || _disposed)
        {
            return (readings, series);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (IHardware device in _hardware)
        {
            if (_faults.GetValueOrDefault(device) >= MaxFaults)
            {
                continue;
            }

            foreach (ISensor sensor in SensorMap.AllSensors(device))
            {
                if (LhmAdvancedMap.FamilyOf(device.HardwareType, sensor.SensorType) is not string family
                    || AdvancedMetricCatalog.Find(family) is not { } metric
                    || LhmAdvancedMap.KeyOf(sensor.Identifier?.ToString(), sensor.SensorType) is not string key
                    || sensor.Value is not float value
                    || !float.IsFinite(value))
                {
                    continue;
                }

                string name = AdvancedMetricCatalog.InstanceName(metric, key);
                if (!seen.Add(name))
                {
                    // Two sensors whose identifiers shorten to one key: the first keeps the name, the second is not read.
                    continue;
                }

                readings.Add(new AdvancedReading(name, value));
                series.Add(new AdvancedSeriesInfo(name, family, LhmAdvancedMap.LabelOf(sensor)));
            }
        }

        return (readings, series);
    }

    /// <summary>
    /// The facts the opened groups carry: the motherboard's model and its sensor chip, and the memory devices by name.
    /// Empty where those groups are not open.
    /// </summary>
    public IReadOnlyList<AdvancedFact> ReadFacts()
    {
        var facts = new List<AdvancedFact>();
        if (!_open || _disposed)
        {
            return facts;
        }

        if (_hardware.FirstOrDefault(static h => h.HardwareType == HardwareType.Motherboard) is { } board)
        {
            if (!string.IsNullOrWhiteSpace(board.Name))
            {
                facts.Add(new AdvancedFact("lhm.board.model", Instance: null, AdvancedValue.OfText(board.Name.Trim())));
            }

            string[] chips = [.. board.SubHardware.Where(static s => s.HardwareType is HardwareType.SuperIO or HardwareType.EmbeddedController).Select(static s => s.Name?.Trim() ?? string.Empty).Where(static n => n.Length > 0)];
            if (chips.Length > 0)
            {
                facts.Add(new AdvancedFact("lhm.board.sensor_chip", Instance: null, AdvancedValue.OfText(string.Join(", ", chips))));
            }
        }

        AdvancedRow[] modules = [.. _hardware.Where(static h => h.HardwareType == HardwareType.Memory && !string.IsNullOrWhiteSpace(h.Name)).Select(static h => new AdvancedRow(h.Name.Trim(), string.Empty))];
        if (modules.Length > 0)
        {
            facts.Add(new AdvancedFact("lhm.memory.modules", Instance: null, AdvancedValue.OfRows(modules)));
        }

        return facts;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_open)
        {
            try
            {
                _computer.Close();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Closing a library that already faulted must not take the reader's thread with it.
                LastFault = $"close: {e.GetType().Name}: {e.Message}";
            }
        }
    }

    private static void Update(IHardware hardware, int depth)
    {
        hardware.Update();
        if (depth >= 4)
        {
            return;
        }

        foreach (IHardware sub in hardware.SubHardware)
        {
            Update(sub, depth + 1);
        }
    }
}
