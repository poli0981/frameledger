// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// Mirror of <c>FlPsSample</c> (<c>fl_process_stats.h</c>, beta.12, D43). <see cref="Size"/> is set by the caller and checked
/// by the DLL, so a drift between the two is refused (<c>FL_PS_BAD_SIZE</c>) rather than read; <c>ProcessStatsMirrorTests</c>
/// compares <c>Marshal.SizeOf</c> against <c>FlPsSampleSize()</c> as well.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "P/Invoke mirror of a native struct; never compared.")]
[StructLayout(LayoutKind.Sequential)]
public struct ProcessStatsSample
{
    public uint Size;

    public uint Present;

    public ulong PrivateWorkingSetBytes;

    public ulong WorkingSetBytes;

    public ulong PeakWorkingSetBytes;

    public ulong CommitBytes;

    public ulong GpuDedicatedBytes;

    public ulong GpuSharedBytes;

    public uint GpuInstances;

    public int RamStatus;

    public uint RamError;

    public int GpuStatus;

    public uint PdhStatus;

    public uint Reserved0;

    public uint Reserved1;

    public uint Reserved2;
}
