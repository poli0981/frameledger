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
/// <b>Amended 2026-11-12: a user of <see cref="LhmLibrary"/>, not the owner of a <c>Computer</c>.</b> The library keeps
/// part of its state per process, and closing one <c>Computer</c> took it from under the others — found in the first
/// sessions recorded as administrator with PawnIO. Opening and closing now go through the process's one
/// <see cref="LhmLibrary"/>, which serialises them and parks a closed <c>Computer</c> while another is in use.
/// </para>
/// <para>
/// Consumed <b>unmodified</b>, as an ordinary NuGet package reference. MPL-2.0 §3.3 is what
/// makes that combinable with GPLv3; modifying a file of it would change the obligation
/// (<c>legal/THIRD_PARTY_NOTICES.md</c> §GPL-3.0 compatibility).
/// </para>
/// </remarks>
public sealed class LhmComputerAdapter : ILhmComputer
{
    private readonly LhmLibrary _library;
    private readonly LhmGroups _groups;
    private ILhmComputer? _computer;

    public LhmComputerAdapter(bool enableCpuAndMemory, bool enableGpu = true)
        : this((enableGpu ? LhmGroups.Gpu : LhmGroups.None) | (enableCpuAndMemory ? LhmGroups.Cpu | LhmGroups.Memory : LhmGroups.None))
    {
    }

    /// <summary>The groups <paramref name="groups"/> names, and no other (D103).</summary>
    public LhmComputerAdapter(LhmGroups groups)
        : this(groups, LhmLibrary.Process)
    {
    }

    /// <summary>The same, over a library of the caller's: the tests', which opens no driver.</summary>
    public LhmComputerAdapter(LhmGroups groups, LhmLibrary library)
    {
        _groups = groups;
        _library = library ?? throw new ArgumentNullException(nameof(library));
    }

    /// <summary>The tree of the open <c>Computer</c>; empty before <see cref="Open"/> and after <see cref="Close"/>.</summary>
    public IReadOnlyList<IHardware> Hardware => _computer?.Hardware ?? [];

    /// <summary>Opens once: a second call while open changes nothing.</summary>
    public void Open() => _computer ??= _library.Open(_groups);

    public void Update() => _computer?.Update();

    /// <summary>Lets go once: the library decides whether the <c>Computer</c> is closed now or parked.</summary>
    public void Close()
    {
        if (_computer is { } computer)
        {
            _computer = null;
            _library.Close(_groups, computer);
        }
    }
}
