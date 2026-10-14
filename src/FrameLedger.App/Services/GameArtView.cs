// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Windows.Media;

namespace FrameLedger.App.Services;

/// <summary>
/// A game's artwork as its page shows it (beta.23, D60): the cover slot's image (the user's own, or the store's portrait
/// cover, or its landscape header), the store's own cover for the edit dialog's "use the store's image", the banner, the
/// band's colour (null: untinted), and where the images came from, for the tooltip.
/// </summary>
public sealed record GameArtView(ImageSource? Cover, bool CoverIsOwn, bool CoverIsLandscape, ImageSource? StoreCover, ImageSource? Hero, Color? Tint, string Source);
