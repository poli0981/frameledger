using FrameLedger.Application.Persistence;
using FrameLedger.Application.Watch;
using FrameLedger.Domain.Consent;

namespace FrameLedger.Application.Import;

/// <summary>
/// FR-1.2 in two halves (P4 PR-4): <see cref="DiscoverAsync"/> reads every installed store's library into a review
/// list — the store's executable where it names one, the locator's guess where it does not, and whether the ledger
/// already holds the row — and <see cref="ImportAsync"/> adds the rows the user ticked. <b>Import never enables
/// hooking for anything</b>: a row lands exactly as File ▸ Add game… lands it, hooking off, and the store's
/// platform, id and version are written under the provenance rule so a later correction by the user sticks.
/// Nothing is launched, nothing is fetched (CLAUDE.md rule 8: store metadata is opt-in and this reads only what
/// the launcher already wrote to disk).
/// </summary>
public sealed class LibraryImporter
{
    private readonly IReadOnlyList<IStoreLibrarySource> _sources;
    private readonly IGameRepository _games;
    private readonly IExecutableIdentitySource _identity;
    private readonly IExecutableLocator _locator;
    private readonly Action<string> _log;

    public LibraryImporter(IEnumerable<IStoreLibrarySource> sources, IGameRepository games, IExecutableIdentitySource identity, IExecutableLocator locator, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(sources);
        _sources = [.. sources];
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Every store's titles, one candidate per executable (the first store to name a path keeps it), in store then name order.</summary>
    public async ValueTask<IReadOnlyList<ImportCandidate>> DiscoverAsync(CancellationToken ct = default)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<ImportCandidate> candidates = [];
        foreach (IStoreLibrarySource source in _sources)
        {
            IReadOnlyList<StoreGame> games;
            try
            {
                games = await source.ListAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                _log($"import: {source.Platform} library unreadable — {ex.Message}");
                continue;
            }

            foreach (StoreGame game in games.OrderBy(static g => g.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                bool guessed = game.ExePath is null;
                string? exe = game.ExePath ?? _locator.PickPrimary(game.InstallDirectory);
                if (exe is not null)
                {
                    exe = _identity.Normalise(exe);
                    if (!seen.Add(exe))
                    {
                        continue;    // the same executable under two stores: one row, the first store's
                    }
                }

                // A store's record can outlive the install (a title moved or uninstalled by hand): the review says so
                // rather than the import failing later.
                bool missing = exe is not null && _identity.Read(exe) is null;
                bool already = exe is not null && await _games.FindAsync(exe, ct).ConfigureAwait(false) is { InLibrary: true };
                candidates.Add(new ImportCandidate(game, exe, already, guessed, missing));
            }

            _log($"import: {source.Platform} — {games.Count} title(s)");
        }

        return candidates;
    }

    /// <summary>Add the ticked candidates. A row that appeared meanwhile is left as it is; the store's facts are applied either way.</summary>
    public async ValueTask<ImportReport> ImportAsync(IReadOnlyList<ImportCandidate> selected, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(selected);
        int added = 0;
        int skipped = 0;
        foreach (ImportCandidate c in selected)
        {
            ct.ThrowIfCancellationRequested();
            if (c.ExePath is null || _identity.Read(c.ExePath) is not { } fingerprint)
            {
                skipped++;
                _log($"import: {c.Game.Name} skipped — no readable executable");
                continue;
            }

            GameRow? before = await _games.FindAsync(c.ExePath, ct).ConfigureAwait(false);
            GameRow row = await _games.EnsureAsync(fingerprint, c.Game.Name, ct).ConfigureAwait(false);
            _ = await _games.ApplyStoreMetadataAsync(row.Id, new StoreMetadata { Platform = c.Game.Platform, StoreId = c.Game.StoreId, GameVersion = c.Game.Version }, ct).ConfigureAwait(false);
            if (before is { InLibrary: true })
            {
                skipped++;
            }
            else
            {
                added++;
                _log($"import: added {c.Game.Name} ({c.Game.Platform} {c.Game.StoreId}) — hooking off");
            }
        }

        return new ImportReport(added, skipped);
    }
}
