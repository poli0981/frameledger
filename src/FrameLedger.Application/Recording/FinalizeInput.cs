using FrameLedger.Application.Persistence;
using FrameLedger.Application.Telemetry;

namespace FrameLedger.Application.Recording;

/// <summary>What the finalizer is handed: the row's identity half, and — for a hooked session — what the ring produced.</summary>
public sealed record FinalizeInput
{
    /// <summary>Identity, times, tier, mode, exit status and notes, filled by the recorder; every measured column still null.</summary>
    public required SessionRow Skeleton { get; init; }

    /// <summary>Null for a Tier-2 session: duration, sensors and the reason, and nothing else (<c>04_CAPTURE</c>).</summary>
    public AggregationInput? Hooked { get; init; }

    public IReadOnlyList<TelemetrySample> Sensors { get; init; } = [];

    /// <summary><c>06_DATA_MODEL</c> §Retention: raw blobs for the last N sessions per game.</summary>
    public int RetentionKeep { get; init; } = SessionFinalizer.DefaultRetentionKeep;

    /// <summary><c>04_CAPTURE</c> §Discard rule: shorter than this is dropped. The Agent keeps the default; the unshipped host lowers it for bounded operator captures.</summary>
    public TimeSpan MinimumSessionLength { get; init; } = SessionFinalizer.MinimumSessionLength;

    /// <summary>
    /// The normalised executable the session ran (2026-09-23): how the finalizer finds the session's row when the one it
    /// started under is gone — removed while it ran, or its id reused by another game (<c>games.id</c> has no
    /// AUTOINCREMENT). Null keeps the old behaviour: the skeleton's <c>GameId</c>, or nothing when that row is gone.
    /// </summary>
    public string? ExePath { get; init; }
}
