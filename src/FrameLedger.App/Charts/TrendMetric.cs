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
}
