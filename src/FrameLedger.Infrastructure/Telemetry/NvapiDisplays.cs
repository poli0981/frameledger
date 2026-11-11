// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// Mirror of <c>FlNvDisplays</c> (<c>fl_nvapi_bridge.h</c>, ABI 3, D101): the displays connected to the first graphics card.
/// </summary>
// CA1815: a window onto a native struct, never compared — the same call NvapiSample makes.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "P/Invoke mirror of a native struct; never compared.")]
[StructLayout(LayoutKind.Sequential)]
public struct NvapiDisplays
{
    /// <summary><c>FL_NV_MAX_DISPLAYS</c>.</summary>
    public const int MaxDisplays = 8;

    public uint Size;

    /// <summary><c>NvAPI_GPU_GetConnectedDisplayIds</c>' status, verbatim.</summary>
    public int NvapiStatus;

    public uint Count;

    public uint Reserved;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxDisplays)]
    public NvapiDisplay[] Displays;
}
