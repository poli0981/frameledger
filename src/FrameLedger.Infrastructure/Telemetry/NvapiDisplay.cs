// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// Mirror of <c>FlNvDisplay</c> (<c>fl_nvapi_bridge.h</c>, ABI 3, D101): what one connected display can do.
/// <see cref="DisplayId"/> is NVAPI's handle for this boot's topology — never stored, never shown (D104).
/// </summary>
// CA1815: a window onto a native struct, never compared — the same call NvapiSample makes.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "P/Invoke mirror of a native struct; never compared.")]
[StructLayout(LayoutKind.Sequential)]
public struct NvapiDisplay
{
    /// <summary><c>FL_NV_DISPLAY_HDR</c>: <see cref="HdrFlags"/> and the three luminances carry a value.</summary>
    public const uint HasHdr = 0x1;

    /// <summary><c>FL_NV_DISPLAY_CAPS</c>: <see cref="CapsFlags"/> carries a value.</summary>
    public const uint HasCaps = 0x2;

    /// <summary><c>FL_NV_DISPLAY_VRR</c>: <see cref="VrrFlags"/> carries a value.</summary>
    public const uint HasVrr = 0x4;

    /// <summary><c>FL_NV_DISPLAY_ADAPTIVE_SYNC</c>: <see cref="AdaptiveSyncFlags"/> and <see cref="MaxFrameIntervalUs"/> carry a value.</summary>
    public const uint HasAdaptiveSync = 0x8;

    public const uint HdrSt2084 = 0x01;
    public const uint HdrEdr = 0x04;
    public const uint HdrDolbyVision = 0x10;
    public const uint HdrHdr10Plus = 0x20;
    public const uint HdrHdr10PlusGaming = 0x40;

    public const uint CapsVrr = 0x01;
    public const uint CapsUlmb = 0x02;
    public const uint CapsTrueGsync = 0x04;
    public const uint CapsVrrNow = 0x10;

    public const uint VrrEnabled = 0x01;
    public const uint VrrPossible = 0x02;
    public const uint VrrRequested = 0x04;
    public const uint VrrDisplayMode = 0x10;

    public const uint AdaptiveSyncDisabled = 0x01;

    public uint DisplayId;

    public uint Present;

    public uint Active;

    public uint HdrFlags;

    public uint LuminanceMax;

    public uint LuminanceMinTenThousandths;

    public uint LuminanceMaxFrameAverage;

    public uint CapsFlags;

    public uint VrrFlags;

    public uint AdaptiveSyncFlags;

    public uint MaxFrameIntervalUs;

    public uint Reserved;
}
