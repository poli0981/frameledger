using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Services;

/// <summary>FR-9.2: the session JSON — metadata, the stored aggregates (the row as is), the segments, the user's annotation.</summary>
public sealed record SessionExportDocument
{
    public required string Schema { get; init; }

    public required Guid SessionGuid { get; init; }

    public required string Game { get; init; }

    public required string ExePath { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset EndedAt { get; init; }

    public required double DurationSeconds { get; init; }

    public required int CaptureTier { get; init; }

    public required string CaptureMode { get; init; }

    public required string ExitStatus { get; init; }

    public string? Api { get; init; }

    public string? PresentMode { get; init; }

    public HardwareSnapshot? Hardware { get; init; }

    public required SessionRow Aggregates { get; init; }

    public required IReadOnlyList<SegmentRow> Segments { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string? Notes { get; init; }
}
