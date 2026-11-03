// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Art;

/// <summary>
/// The artwork a store's own app keeps on this PC for one game (beta.23, owner decision D60): the full paths of its portrait
/// cover, its wide banner and its landscape header, each null when that app keeps none — and since beta.33 a square logo,
/// what an Xbox game's package ships instead of a portrait cover (its <c>Square480x480Logo</c>). FrameLedger downloads
/// none of it and copies none of it; it shows these files from where the store's app or the game put them.
/// </summary>
public sealed record GameArtFiles(string? Cover, string? Hero, string? Header, string? Square = null)
{
    /// <summary>No artwork: the store keeps none, is not installed, or the id is not one of its own.</summary>
    public static GameArtFiles None { get; } = new(null, null, null);

    public bool IsEmpty => Cover is null && Hero is null && Header is null && Square is null;
}
