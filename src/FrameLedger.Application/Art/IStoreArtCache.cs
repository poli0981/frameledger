// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Import;

namespace FrameLedger.Application.Art;

/// <summary>
/// One store's artwork for a game on this PC (beta.23, owner decision D60): Steam's <c>appcache\librarycache</c>, GOG
/// GALAXY's <c>webcache</c>, and since beta.33 the logo and splash image an Xbox game ships in its own package folder.
/// Read-only. A store that is not installed, an id that is not of the store's own shape, or a folder with nothing in it
/// answers <see cref="GameArtFiles.None"/>, never an error. Which store installed a game is asked of the stores' own
/// records, as Play asks (<c>Play.StoreRecords.Owning</c>) — never of the row's platform alone.
/// </summary>
public interface IStoreArtCache
{
    /// <summary><c>steam</c> · <c>gog</c> · <c>xbox</c> — <c>games.platform</c>'s vocabulary.</summary>
    string Platform { get; }

    /// <summary>
    /// The artwork for <paramref name="record"/>, the owning store's record of the game: the whole record since beta.33,
    /// because an Xbox game's pictures are in its install folder, not keyed by its id.
    /// </summary>
    GameArtFiles Find(StoreGame record);
}
