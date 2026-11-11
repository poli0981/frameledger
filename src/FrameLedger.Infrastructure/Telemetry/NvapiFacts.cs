// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// Mirror of <c>FlNvFacts</c> (<c>fl_nvapi_bridge.h</c>, ABI 3, D101): what is true of the first graphics card whatever
/// is running. A clear bit of <see cref="Present"/> is N/A, never 0. <c>NvapiBridgeMirrorTests</c> holds its size to
/// the bridge's own <c>FlNvFactsSize</c>.
/// </summary>
// CA1815: a window onto a native struct, never compared — the same call NvapiSample makes.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "P/Invoke mirror of a native struct; never compared.")]
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct NvapiFacts
{
    public uint Size;

    public uint Present;

    public uint GpuIndex;

    public uint GpuCount;

    public uint GpuType;

    public uint SystemType;

    public uint External;

    public uint BusType;

    public uint PciDeviceId;

    public uint PciSubSystemId;

    public uint PciRevisionId;

    public uint PciExtDeviceId;

    public uint ArchId;

    public uint ArchImplementation;

    public uint ArchRevision;

    public uint CoreCount;

    public uint RtCores;

    public uint TensorCores;

    public uint RamBusWidth;

    public uint DriverFlags;

    public float ClockBaseCoreMhz;

    public float ClockBaseMemoryMhz;

    public float ClockBoostCoreMhz;

    public float ClockBoostMemoryMhz;

    public float TempDefaultMaxC;

    public uint FbPhysicalKb;

    public uint FbVirtualKb;

    public uint Reserved0;

    public ulong VramDedicatedBytes;

    public ulong VramDedicatedAvailableBytes;

    public ulong VramSystemBytes;

    public ulong VramSharedBytes;

    public uint Reserved1;

    public uint Reserved2;

    public uint Reserved3;

    public uint Reserved4;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Vbios;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string BaseBranch;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string InterfaceVersion;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string ChipsetVendor;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string ChipsetName;

    public readonly bool Has(NvapiFactField field) => (Present & (uint)field) != 0;
}
