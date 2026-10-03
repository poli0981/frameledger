// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using FrameLedger.Application.Persistence;

namespace FrameLedger.App.Services;

/// <summary>One library card's data: the row and its sessions in aggregate (null when it has none).</summary>
public sealed record GameCard(GameRow Row, GameSessionSummary? Summary);
