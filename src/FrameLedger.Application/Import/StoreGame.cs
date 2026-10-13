// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

namespace FrameLedger.Application.Import;

/// <summary>
/// One installed title as a store's own records describe it (FR-1.2, P4 PR-4): the platform id in
/// <c>games.platform</c>'s vocabulary, the store's id for it, its display name, where it is installed, the
/// executable when the store names one (GOG and Epic do; Steam and itch do not), and the store's version string.
/// Read from local launcher files and registry keys only — nothing is fetched (CLAUDE.md rule 8).
/// </summary>
/// <remarks>
/// <c>LaunchKey</c> (beta.21, Play, D59): what the store's own launch link names where that is more than the store id —
/// the Epic Games Launcher's <c>CatalogNamespace:CatalogItemId:AppName</c>; null for every other store.
/// </remarks>
public sealed record StoreGame(string Platform, string StoreId, string Name, string InstallDirectory, string? ExePath, string? Version,
    string? LaunchKey = null);
