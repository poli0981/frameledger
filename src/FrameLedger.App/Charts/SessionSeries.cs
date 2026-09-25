namespace FrameLedger.App.Charts;

/// <summary>
/// A session's per-present series decoded for the charts and the export: every present of the charted stream in drained
/// order with its time from the stream's first present, its interval, whether it was generated, whether a gap sat before
/// it; the application-frame view with the stutter flags over it; the segments on the time axis; the sensor series.
/// Decoded once per window, decimated per draw.
/// </summary>
/// <remarks>
/// <b>One stream (beta.8).</b> A session that presented to more than one swapchain is charted on the one its statistics are
/// over (<c>SegmentBuilder.DominantStream</c>: identified first, then the most presents) — the stored intervals are per
/// swapchain, and interleaving two streams' intervals on one time axis drew neither.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "a data carrier for the charts: ScottPlot draws double[] and a 500k-frame session is decimated per draw, not copied per read")]
public sealed record SessionSeries
{
    /// <summary>Seconds from the stream's first present, per present (a cumulative sum of the intervals; a gap adds none).</summary>
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

    /// <summary>The time of each application frame's present, seconds.</summary>
    public required double[] AppTimesS { get; init; }

    /// <summary>
    /// Each application frame's time, ms: from the previous application frame's present to its own (beta.8) — the generated
    /// presents between them add no interval, and a gap breaks the chain. Every present's interval where nothing was generated.
    /// </summary>
    public required double[] AppFrameTimesMs { get; init; }

    /// <summary>Index into the per-present arrays for each application frame.</summary>
    public required int[] AppPresentIndex { get; init; }

    /// <summary>
    /// Every present was generated — frame generation counted and no application frame's token arrived (the <c>no_evaluations</c>
    /// refusal) — so the application-frame view is every present instead, and the chart says so.
    /// </summary>
    public bool NoApplicationFrames { get; init; }

    /// <summary>Per application frame, from <c>Domain.Metrics.StutterDetector</c>; null when the session is too short for the window.</summary>
    public bool[]? Stutter { get; init; }

    public required IReadOnlyList<SegmentSpan> Segments { get; init; }

    /// <summary>
    /// The sensor series. On the frames' axis — seconds from the first present, negative before it — when
    /// <see cref="SensorsAligned"/>; from the session's start otherwise.
    /// </summary>
    public required IReadOnlyList<SensorSeries> Sensors { get; init; }

    /// <summary>
    /// The sensors are placed on the frames' time axis: the blob said where the first present sits on the sensors' clock
    /// (schema 0013). A session recorded before cannot draw them over its frames.
    /// </summary>
    public bool SensorsAligned { get; init; }

    public int Presents => TimesS.Length;

    public bool HasGenerated => Generated.Any(static g => g);

    public double DurationS => TimesS.Length == 0 ? 0 : TimesS[^1];

    /// <summary>A series with no frames: a session that was not hooked still has its sensors (beta.8).</summary>
    public static SessionSeries SensorsOnly(IReadOnlyList<SensorSeries> sensors) => new()
    {
        TimesS = [],
        FrameTimesMs = [],
        Generated = [],
        Gap = [],
        AppTimesS = [],
        AppFrameTimesMs = [],
        AppPresentIndex = [],
        Segments = [],
        Sensors = sensors,
    };
}
