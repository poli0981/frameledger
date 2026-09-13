namespace FrameLedger.Application.Persistence;

/// <summary>
/// The user-editable fields of a game (FR-1.3; <c>06_DATA_MODEL</c> §Writer ownership: the UI's part of
/// <c>games</c>). Written wholesale: the page loads the row, the user edits, the page saves all of these; any
/// field whose value changed is marked <c>user</c> in <c>field_provenance</c>, which is the rule that keeps a
/// later detection pass from overwriting it (<c>05_DETECTION</c> §Caching).
/// </summary>
public sealed record GameMetadata
{
    public required string Name { get; init; }

    /// <summary><c>steam|gog|epic|itch|none</c>.</summary>
    public string Platform { get; init; } = "none";

    public string? StoreId { get; init; }

    public string? Engine { get; init; }

    public string? EngineVersion { get; init; }

    public string? Publisher { get; init; }

    public string? GameVersion { get; init; }

    public string? CoverPath { get; init; }

    public string? Notes { get; init; }

    public static readonly IReadOnlyList<string> Platforms = ["steam", "gog", "epic", "itch", "none"];

    public static GameMetadata Of(GameRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new GameMetadata
        {
            Name = row.Name,
            Platform = row.Platform,
            StoreId = row.StoreId,
            Engine = row.Engine,
            EngineVersion = row.EngineVersion,
            Publisher = row.Publisher,
            GameVersion = row.GameVersion,
            CoverPath = row.CoverPath,
            Notes = row.Notes,
        };
    }
}
