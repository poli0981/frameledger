// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Art;

/// <summary>
/// One store app's own artwork folder on this PC (beta.23, owner decision D60): Steam's <c>appcache\librarycache</c>, GOG
/// GALAXY's <c>webcache</c>. Read-only. A store that is not installed, an id that is not of the store's own shape, or a
/// folder with nothing in it answers <see cref="GameArtFiles.None"/>, never an error. Which store installed a game is
/// asked of the stores' own records, as Play asks (<c>Play.StoreRecords.Owning</c>) — never of the row's platform alone.
/// </summary>
public interface IStoreArtCache
{
    /// <summary><c>steam</c> · <c>gog</c> — <c>games.platform</c>'s vocabulary.</summary>
    string Platform { get; }

    GameArtFiles Find(string storeId);
}
