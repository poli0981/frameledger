// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Import;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary>
/// The stores' own records of what they installed (beta.23), the lists Play reads to find the store that installed a game
/// (<c>StoreRecords.Owning</c>): read on the thread pool. Every store's reader is synchronous work — Steam's manifests are
/// files on whatever drive its libraries are on (the owner's: a USB drive), itch's database is copied before it is read —
/// and Play ran all of it on the UI thread at every press.
/// </summary>
public sealed class StoreRecordsReader
{
    private readonly IReadOnlyList<IStoreLibrarySource> _stores;

    public StoreRecordsReader(IEnumerable<IStoreLibrarySource> stores)
    {
        ArgumentNullException.ThrowIfNull(stores);
        _stores = [.. stores];
    }

    /// <summary>
    /// Every record of the stores named in <paramref name="platforms"/> (every store when null), read now. One store whose
    /// records cannot be read is that store's, logged under <paramref name="purpose"/>, never the caller's.
    /// </summary>
    public Task<IReadOnlyList<StoreGame>> ReadAsync(string purpose, IReadOnlyCollection<string>? platforms = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        IStoreLibrarySource[] stores = [.. _stores.Where(s => platforms is null || platforms.Contains(s.Platform, StringComparer.Ordinal))];
        return Task.Run(async () =>
        {
            List<StoreGame> all = [];
            foreach (IStoreLibrarySource store in stores)
            {
                try
                {
                    all.AddRange(await store.ListAsync(ct).ConfigureAwait(false));
                }
                catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
                {
                    Log.Warning("{Purpose}: {Store}'s records could not be read ({Message})", purpose, store.Platform, ex.Message);
                }
            }

            return (IReadOnlyList<StoreGame>)all;
        }, ct);
    }
}
