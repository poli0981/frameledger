namespace FrameLedger.App.Charts;

/// <summary>
/// A session's per-present series decoded for the charts and the export: every present in drained order with its
/// time from the session start, its interval, whether it was generated, whether a gap sat before it; the
/// application-frame view (non-generated, non-gap) with the stutter flags over it; the segments on the time
/// axis; the sensor series. Decoded once per window, decimated per draw.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "a data carrier for the charts: ScottPlot draws double[] and a 500k-frame session is decimated per draw, not copied per read")]
public sealed record SessionSeries
{
    /// <summary>Seconds from the first present, per present (a cumulative sum of the intervals).</summary>
    public required double[] TimesS { get; init; }

    /// <summary>The stored interval per present, ms; 0 on the first present and after a gap.</summary>
    public required float[] FrameTimesMs { get; init; }

    public required bool[] Generated { get; init; }

    public required bool[] Gap { get; init; }

    /// <summary>The stored frame index per present, when the blob carried one.</summary>
    public uint[]? FrameIndex { get; init; }

    /// <summary><c>rt_flags</c> per present, when measured.</summary>
    public byte[]? RtFlags { get; init; }

    public uint[]? DispatchRays { get; init; }

    public ushort[]? PsoCreated { get; init; }

    public uint[]? VramProcMb { get; init; }

    public uint[]? LatencyUs { get; init; }

    /// <summary>Four <c>uint16</c> per present (render w/h, output w/h) when the resolution varied; null otherwise.</summary>
    public ushort[]? RenderRes { get; init; }

    // the application-frame view
    public required double[] AppTimesS { get; init; }

    public required double[] AppFrameTimesMs { get; init; }

    /// <summary>Index into the per-present arrays for each application frame.</summary>
    public required int[] AppPresentIndex { get; init; }

    /// <summary>Per application frame, from <c>Domain.Metrics.StutterDetector</c>; null when the session is too short for the window.</summary>
    public bool[]? Stutter { get; init; }

    public required IReadOnlyList<SegmentSpan> Segments { get; init; }

    public required IReadOnlyList<SensorSeries> Sensors { get; init; }

    public int Presents => TimesS.Length;

    public bool HasGenerated => Generated.Any(static g => g);

    public double DurationS => TimesS.Length == 0 ? 0 : TimesS[^1];
}
