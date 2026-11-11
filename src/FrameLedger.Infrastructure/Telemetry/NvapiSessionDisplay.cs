// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// Mirror of <c>FlNvSessionDisplayRead</c> (<c>fl_nvapi_bridge.h</c>, ABI 3, D101): the state of the display a game
/// presents on, and the colour space the driver reports for the game's process.
/// </summary>
// CA1815: a window onto a native struct, never compared — the same call NvapiSample makes.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "P/Invoke mirror of a native struct; never compared.")]
[StructLayout(LayoutKind.Sequential)]
public struct NvapiSessionDisplay
{
    /// <summary><c>FL_NV_SESSION_VRR</c>: <see cref="VrrFlags"/> carries a value.</summary>
    public const uint HasVrr = 0x1;

    /// <summary><c>FL_NV_SESSION_COLORSPACE</c>: <see cref="ColorSpace"/> carries a value.</summary>
    public const uint HasColorSpace = 0x2;

    public uint Size;

    /// <summary><c>NvAPI_DISP_GetDisplayIdByDisplayName</c>'s status: not 0, the display was not found and nothing else was asked.</summary>
    public int NvapiStatus;

    public uint Present;

    /// <summary>The <c>FL_NV_VRR_*</c> bits — <see cref="NvapiDisplay.VrrEnabled"/> and its neighbours.</summary>
    public uint VrrFlags;

    /// <summary><c>NV_COLORSPACE_TYPE</c>: 0 sRGB, 1 linear FP16 with sRGB primaries, 12 BT.2100.</summary>
    public uint ColorSpace;

    public int ColorSpaceStatus;

    public uint Reserved0;

    public uint Reserved1;
}
