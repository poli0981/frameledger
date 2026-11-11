// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>The heading an advanced metric is shown under.</summary>
public enum AdvancedGroup
{
    /// <summary>Not stated. No entry of the catalog carries it.</summary>
    None = 0,

    Gpu = 1,
    GpuMemory = 2,
    Driver = 3,
    Display = 4,
    Cpu = 5,
    Board = 6,
    Memory = 7,
    Storage = 8,
    Upscaling = 9,
    FrameGeneration = 10,
    Latency = 11,
}
