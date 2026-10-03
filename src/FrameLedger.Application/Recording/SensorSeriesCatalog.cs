// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Telemetry;

namespace FrameLedger.Application.Recording;

/// <summary>
/// Every 1 Hz series a session stores in <c>sensor_blobs</c>, by name, with the field of <see cref="TelemetrySample"/> it is
/// read from (<c>06_DATA_MODEL</c> §sensor_blobs). One list for both consumers — the finalizer's blobs and the per-series
/// statistics in <c>sessions.sensor_stats_json</c> (beta.12) — so a series cannot be stored without its statistics or the
/// other way round.
/// </summary>
public static class SensorSeriesCatalog
{
    public const string GpuTemp = "gpu_temp";
    public const string GpuHotspot = "gpu_hotspot";
    public const string GpuLoad = "gpu_load";
    public const string GpuPower = "gpu_power";
    public const string VramAdapter = "vram_adapter";
    public const string CpuLoad = "cpu_load";
    public const string CpuTemp = "cpu_temp";
    public const string RamSystem = "ram_mb";

    /// <summary>The game process's dedicated GPU memory, MiB (beta.12, D43).</summary>
    public const string GameVramDedicated = "game_vram_dedicated";

    /// <summary>The game process's shared GPU memory, MiB.</summary>
    public const string GameVramShared = "game_vram_shared";

    /// <summary>The game process's private working set, MiB — Task Manager's "Memory".</summary>
    public const string GameRamPrivate = "game_ram_private";

    /// <summary>The game process's whole working set, MiB.</summary>
    public const string GameRamWorkingSet = "game_ram_ws";

    /// <summary>The game process's commit charge, MiB.</summary>
    public const string GameCommit = "game_commit";

    /// <summary>The series in storage order. The first eight predate beta.12; their names are on disk and never change.</summary>
    public static IReadOnlyList<(string Name, Func<TelemetrySample, double?> Select)> All { get; } =
    [
        (GpuTemp, static s => s.Sample.TempCoreC),
        (GpuHotspot, static s => s.Sample.TempHotspotC),
        (GpuLoad, static s => s.Sample.LoadPct),
        (GpuPower, static s => s.Sample.PowerW),
        (VramAdapter, static s => s.Sample.VramAdapterMb),
        (CpuLoad, static s => s.System.CpuLoadPct),
        (CpuTemp, static s => s.System.CpuTempC),
        (RamSystem, static s => s.System.RamUsedMb),
        (GameVramDedicated, static s => s.Game.VramDedicatedMb),
        (GameVramShared, static s => s.Game.VramSharedMb),
        (GameRamPrivate, static s => s.Game.RamPrivateMb),
        (GameRamWorkingSet, static s => s.Game.RamWorkingSetMb),
        (GameCommit, static s => s.Game.CommitMb),
    ];
}
