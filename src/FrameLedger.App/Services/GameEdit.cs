// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Services;

/// <summary>
/// What the edit dialog answered: the metadata to store — its <c>CoverPath</c> null when the user went back to the store's
/// image — and the image file the user picked, which is copied only once the dialog closed with Save (beta.23).
/// </summary>
public sealed record GameEdit(GameMetadata Metadata, string? PickedImage);
