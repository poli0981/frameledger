// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Advanced;

/// <summary>The vendor interface an advanced metric comes from. An entry's id starts with the source's prefix.</summary>
public enum AdvancedSource
{
    /// <summary>Not stated. No entry of the catalog carries it.</summary>
    None = 0,

    /// <summary>NVAPI (MIT, NVIDIA only) — <c>nvapi.</c></summary>
    Nvapi = 1,

    /// <summary>LibreHardwareMonitorLib (MPL-2.0, every vendor; its CPU, board, memory and storage groups through PawnIO) — <c>lhm.</c></summary>
    Lhm = 2,

    /// <summary>NVIDIA Streamline (MIT headers), read inside the game only — <c>sl.</c></summary>
    Streamline = 3,

    /// <summary>AMD FidelityFX (MIT headers), read inside the game only — <c>ffx.</c></summary>
    FidelityFx = 4,

    /// <summary>NVIDIA NGX, whose SDK licence forbids vendoring and re-declaring — <c>ngx.</c></summary>
    Ngx = 5,

    /// <summary>Intel XeSS, whose SDK licence forbids the same — <c>xess.</c></summary>
    Xess = 6,
}
