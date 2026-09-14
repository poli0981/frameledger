namespace FrameLedger.Application.Persistence;

/// <summary>
/// What a store's own records say about a game (FR-1.2, P4 PR-4), persisted through
/// <see cref="IGameRepository.ApplyStoreMetadataAsync"/> under the same provenance rule as detection: a value the
/// user typed is never overwritten, a value a store or a scan wrote before is refreshed, an empty field is filled
/// and badged <c>detected</c>. Null = the store did not say; the field is left as it is.
/// </summary>
public sealed record StoreMetadata
{
    /// <summary><c>steam</c> · <c>gog</c> · <c>epic</c> · <c>itch</c>.</summary>
    public required string Platform { get; init; }

    public string? StoreId { get; init; }

    public string? GameVersion { get; init; }
}
