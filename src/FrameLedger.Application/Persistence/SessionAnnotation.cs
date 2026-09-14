using FrameLedger.Domain.Metrics;

namespace FrameLedger.Application.Persistence;

/// <summary>
/// The UI-owned row beside a session (<c>session_annotations</c>): the user's tags and notes, and since
/// schema 0002 the three FR-8.3 per-session overrides — on this row and not on <c>sessions</c>, because
/// <c>06_DATA_MODEL</c> §Writer ownership gives <c>sessions</c> to the Agent and this table to the UI.
/// </summary>
public sealed record SessionAnnotation
{
    public required long SessionId { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string? Notes { get; init; }

    public Tri? RtOverride { get; init; }

    public Tri? PtOverride { get; init; }

    public Tri? RrOverride { get; init; }

    /// <summary>True when the row says nothing — the repository deletes such a row rather than storing an empty one.</summary>
    public bool IsEmpty => Tags.Count == 0 && string.IsNullOrWhiteSpace(Notes) && RtOverride is null && PtOverride is null && RrOverride is null;
}
