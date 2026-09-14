namespace FrameLedger.Application.Import;

/// <summary>
/// One store's installed library, read from its local files (FR-1.2, <c>05_DETECTION</c> §Platform signatures &amp;
/// metadata: Steam's <c>libraryfolders.vdf</c> + <c>appmanifest_*.acf</c>, GOG's registry keys, Epic's
/// <c>Manifests\*.item</c>, itch's receipts). A store that is not installed answers an empty list, never an error;
/// a file that will not parse costs that one entry.
/// </summary>
public interface IStoreLibrarySource
{
    /// <summary><c>steam</c> · <c>gog</c> · <c>epic</c> · <c>itch</c> — <c>games.platform</c>'s vocabulary.</summary>
    string Platform { get; }

    ValueTask<IReadOnlyList<StoreGame>> ListAsync(CancellationToken ct = default);
}
