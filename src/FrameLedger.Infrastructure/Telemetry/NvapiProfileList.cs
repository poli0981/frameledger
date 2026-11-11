// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// Mirror of <c>FlNvProfileList</c> (<c>fl_nvapi_bridge.h</c>, ABI 3, D101): which profile was enumerated and how many
/// of its settings were listed. <see cref="Status"/> takes <see cref="NvapiDriverProfile"/>'s three values.
/// </summary>
// CA1815: a window onto a native struct, never compared — the same call NvapiSample makes.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "P/Invoke mirror of a native struct; never compared.")]
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct NvapiProfileList
{
    public uint Size;

    /// <summary><see cref="NvapiDriverProfile.Application"/>, <see cref="NvapiDriverProfile.Global"/> or <see cref="NvapiDriverProfile.Degraded"/>.</summary>
    public int Status;

    public int NvapiStatus;

    /// <summary>Entries written.</summary>
    public uint Count;

    /// <summary>Settings that passed the filter: above <see cref="Count"/>, the buffer was too small.</summary>
    public uint Total;

    public uint Reserved0;

    public uint Reserved1;

    public uint Reserved2;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string ProfileName;
}
