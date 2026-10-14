// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Services;

/// <summary>
/// Everything the detail page shows for one game, loaded in one pass: the row, its sessions newest first, their annotations.
/// (It carried the library's aggregate too, read by summarising EVERY game's sessions to keep one, and read by nothing — gone
/// in beta.23.)
/// </summary>
public sealed record GameDetail(
    GameRow Row,
    IReadOnlyList<SessionRow> Sessions,
    IReadOnlyDictionary<long, SessionAnnotation> Annotations);
