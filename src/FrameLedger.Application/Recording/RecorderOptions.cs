namespace FrameLedger.Application.Recording;

/// <summary>The recorder's per-session policy: the composition's baseline, or what an <see cref="IRecorderPolicy"/> resolved from it at session start.</summary>
public sealed record RecorderOptions
{
    /// <summary>How often the <c>.partial</c> file is flushed (<c>04_CAPTURE</c>: 60 s).</summary>
    public TimeSpan PartialFlushInterval { get; init; } = PartialSessionWriter.DefaultFlushInterval;

    /// <summary>Raw series kept per game after finalize (<c>06_DATA_MODEL</c> §Retention); <see cref="int.MaxValue"/> keeps everything.</summary>
    public int RetentionKeep { get; init; } = SessionFinalizer.DefaultRetentionKeep;

    /// <summary>FR-3.6: shorter sessions are discarded.</summary>
    public TimeSpan MinimumSessionLength { get; init; } = SessionFinalizer.MinimumSessionLength;

    /// <summary>FR-3.5: the sensor poll interval (<c>telemetry.interval_ms</c>, 500–2000).</summary>
    public TimeSpan TelemetryInterval { get; init; } = TimeSpan.FromSeconds(1);
}
