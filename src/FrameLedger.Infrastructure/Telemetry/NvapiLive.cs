// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Runtime.InteropServices;

namespace FrameLedger.Infrastructure.Telemetry;

/// <summary>
/// Mirror of <c>FlNvLive</c> (<c>fl_nvapi_bridge.h</c>, ABI 3, D101): what the first graphics card says now, beside
/// <see cref="NvapiSample"/>. A clear bit of <see cref="Present"/> is N/A, never 0.
/// </summary>
// CA1815: a window onto a native struct, never compared — the same call NvapiSample makes.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "P/Invoke mirror of a native struct; never compared.")]
[StructLayout(LayoutKind.Sequential)]
public struct NvapiLive
{
    public uint Size;

    public uint Present;

    public ulong VramAvailableBytes;

    public ulong VramDedicatedAvailableBytes;

    public ulong EvictionsBytes;

    public ulong EvictionCount;

    public ulong PromotionsBytes;

    public ulong PromotionCount;

    public float LoadFramebufferPct;

    public float LoadVideoPct;

    public float LoadBusPct;

    public float ClockVideoMhz;

    public float TempBoardC;

    public float TempPowerSupplyC;

    public uint Pstate;

    public uint ThrottleReasons;

    public uint EncoderSessions;

    public uint EncoderFps;

    public uint EncoderLatencyUs;

    public uint Reserved0;

    public uint Reserved1;

    public uint Reserved2;

    public uint Reserved3;

    public uint Reserved4;

    public readonly bool Has(NvapiLiveField field) => (Present & (uint)field) != 0;
}
