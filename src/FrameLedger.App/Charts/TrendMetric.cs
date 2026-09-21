namespace FrameLedger.App.Charts;

/// <summary>The Trend tab's selector (<c>08_UI</c> §Games › Trend).</summary>
public enum TrendMetric
{
    /// <summary>Native FPS where frame generation was measured; Presented FPS where it was not (the row's headline, per <c>03_METRICS</c> Avg FPS).</summary>
    Average,

    /// <summary>Displayed FPS — only sessions that measured frame generation have one.</summary>
    Displayed,

    P1Low,

    P01Low,

    MaxGpuTemp,

    /// <summary>The seven below are 2026-09-21's: one game's sessions over time, beyond the frame rate.</summary>
    AvgGpuLoad,

    AvgGpuPower,

    MaxVramProcess,

    AvgCpuLoad,

    MaxCpuTemp,

    AvgRam,

    /// <summary>
    /// The counted ×FG factor. A session whose factor is its STEADY STATE's (CLAUDE.md rule 6, 2026-09-17) describes part
    /// of the session, so it is treated as a mid-session change: left out by default, drawn marked when those are included.
    /// </summary>
    FgFactor,
}
