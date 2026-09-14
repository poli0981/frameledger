namespace FrameLedger.Application.Persistence;

/// <summary>One game's sessions in aggregate, for the library card (<c>08_UI</c> §Games: total playtime, last played) and the Dashboard totals.</summary>
public sealed record GameSessionSummary
{
    public required long GameId { get; init; }

    public required long SessionCount { get; init; }

    /// <summary>Sessions that measured (Tier 1); the rest recorded duration and sensors only.</summary>
    public required long HookedCount { get; init; }

    public required double TotalSeconds { get; init; }

    public DateTimeOffset? LastPlayedAt { get; init; }
}
