// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using LibreHardwareMonitor.Hardware;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// <see cref="ILhmComputer"/> over the real <see cref="Computer"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>IsGpuEnabled</c> is always on; <c>IsCpuEnabled</c> and <c>IsMemoryEnabled</c> only when
/// the caller says so, and the caller may only say so when the Agent is elevated and PawnIO
/// is present (<c>18_GPU_VENDOR_APIS</c> §L2). ~~Nothing else is ever enabled — motherboard,
/// storage, network, battery, PSU and controllers are outside what this product measures,
/// and every group enabled is a driver path that can fault.~~
/// </para>
/// <para>
/// <b>Amended 2026-11-11 (owner decision D103).</b> "Advanced metrics" opens the motherboard group as well, under
/// the same two conditions, through the <see cref="LhmGroups"/> constructor — and only what that enum has a member
/// for can be opened at all: storage, network, battery, PSU and controllers have none, each for the reason written
/// there. The last clause above stands: every group enabled is a driver path that can fault, which is why the
/// reader that opens them counts faults per device and reads on a thread of its own.
/// </para>
/// <para>
/// Consumed <b>unmodified</b>, as an ordinary NuGet package reference. MPL-2.0 §3.3 is what
/// makes that combinable with GPLv3; modifying a file of it would change the obligation
/// (<c>legal/THIRD_PARTY_NOTICES.md</c> §GPL-3.0 compatibility).
/// </para>
/// </remarks>
public sealed class LhmComputerAdapter : ILhmComputer
{
    private readonly Computer _computer;

    public LhmComputerAdapter(bool enableCpuAndMemory, bool enableGpu = true)
    {
        _computer = new Computer
        {
            IsGpuEnabled = enableGpu,
            IsCpuEnabled = enableCpuAndMemory,
            IsMemoryEnabled = enableCpuAndMemory,
        };
    }

    /// <summary>The groups <paramref name="groups"/> names, and no other (D103).</summary>
    public LhmComputerAdapter(LhmGroups groups)
    {
        _computer = new Computer
        {
            IsGpuEnabled = groups.HasFlag(LhmGroups.Gpu),
            IsCpuEnabled = groups.HasFlag(LhmGroups.Cpu),
            IsMemoryEnabled = groups.HasFlag(LhmGroups.Memory),
            IsMotherboardEnabled = groups.HasFlag(LhmGroups.Motherboard),
        };
    }

    public IReadOnlyList<IHardware> Hardware => [.. _computer.Hardware];

    public void Open() => _computer.Open();

    public void Update() => _computer.Accept(new UpdateVisitor());

    public void Close() => _computer.Close();
}
