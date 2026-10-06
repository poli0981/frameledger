// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Telemetry;

/// <summary>
/// What held the graphics card's clocks back on one tick (beta.14, D49), from <see cref="GpuSample.ThrottleReasons"/> — NVAPI's
/// <c>NV_GPU_PERF_DECREASE_REASON_*</c> bits as the L3 bridge passes them through (<c>NvAPI_GPU_GetPerfDecreaseInfo</c>,
/// <c>nvapi.h</c>). No other layer fills the field, so a card that is not NVIDIA's has no answer: null, never "not limited".
/// </summary>
/// <remarks>
/// Each reading is 100 when the bit is set and 0 when it is clear, so the mean of a series is the share of ticks limited, in
/// percent — what <c>sessions.power_limit_pct</c> / <c>thermal_limit_pct</c> store and the stored series' statistics read.
/// </remarks>
public static class GpuLimitReasons
{
    /// <summary><c>NV_GPU_PERF_DECREASE_REASON_THERMAL_PROTECTION</c>: thermal slowdown.</summary>
    public const uint Thermal = 0x1;

    /// <summary><c>NV_GPU_PERF_DECREASE_REASON_POWER_CONTROL</c>: the power limit (power capping / pstate cap).</summary>
    public const uint Power = 0x2;

    /// <summary>100 when <paramref name="reasons"/> includes the power limit, 0 when it does not, null when the tick carried none.</summary>
    public static double? PowerLimited(uint? reasons) => Share(reasons, Power);

    /// <summary>100 when <paramref name="reasons"/> includes thermal slowdown, 0 when it does not, null when the tick carried none.</summary>
    public static double? ThermalLimited(uint? reasons) => Share(reasons, Thermal);

    private static double? Share(uint? reasons, uint bit) => reasons is { } r ? ((r & bit) != 0 ? 100.0 : 0.0) : null;
}
