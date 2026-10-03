// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.App.Charts;

/// <summary>The Trend tab's selector (<c>08_UI</c> §Games › Trend).</summary>
public enum TrendMetric
{
    /// <summary>
    /// Presented FPS — every frame presented — on the sessions where no generated frame was counted (beta.8: "Average" mixed
    /// it with Native in one line). A session that counted generated frames has none here: its presented rate includes them,
    /// the single inflated number CLAUDE.md rule 6 forbids; it is on <see cref="NativeFps"/> and <see cref="Displayed"/>.
    /// </summary>
    PresentedFps,

    /// <summary>Native FPS — the game's own frames — only where frame generation was measured (counted, or measured none).</summary>
    NativeFps,

    /// <summary>Displayed FPS — only sessions that measured frame generation have one.</summary>
    Displayed,

    P1Low,

    P01Low,

    MaxGpuTemp,

    /// <summary>The five below and <see cref="FgFactor"/> are 2026-09-21's: one game's sessions over time, beyond the frame rate.</summary>
    AvgGpuLoad,

    AvgGpuPower,

    AvgCpuLoad,

    MaxCpuTemp,

    /// <summary>The whole PC's memory in use, averaged (the machine's, not the game's), in GB since beta.12.</summary>
    AvgRam,

    /// <summary>
    /// The four below are beta.12's (D43): the game process's own memory, read from outside it, both tiers, in GB — its
    /// dedicated video memory and its private working set, each as the session's median and peak. They replace
    /// <c>MaxVramProcess</c>, which read <c>vram_proc_max_mb</c>: a column nothing ever wrote, so the metric never had a
    /// point.
    /// </summary>
    GameVramMedian,

    GameVramPeak,

    GameRamMedian,

    GameRamPeak,

    /// <summary>
    /// The counted ×FG factor. A session whose factor is its STEADY STATE's (CLAUDE.md rule 6, 2026-09-17) describes part
    /// of the session, so it is treated as a mid-session change: left out by default, drawn marked when those are included.
    /// </summary>
    FgFactor,

    /// <summary>
    /// The three below are beta.10's (<c>03_METRICS</c> §Display mode): the share of the session's observed time in a mode,
    /// every session whatever its tier — the window is read out of process. Exclusive fullscreen and borderless have a point
    /// only where the whole observed time could be told apart; a window that covered its monitor while nothing could ask the
    /// swap chain counts as neither.
    /// </summary>
    DisplayExclusiveShare,

    DisplayBorderlessShare,

    DisplayWindowedShare,
}
