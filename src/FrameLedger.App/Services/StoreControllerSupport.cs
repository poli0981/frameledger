// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FrameLedger.Application.Import;
using FrameLedger.Application.Persistence;
using FrameLedger.Application.Play;
using FrameLedger.Infrastructure.Import;
using Serilog;

namespace FrameLedger.App.Services;

/// <summary>
/// What the store that installed a game lists of controller support (owner decision D163): read from the Steam app's
/// own file on this PC when a game's page is opened, and written to the game's row when it differs. No request is
/// made to any store (CLAUDE.md rule 8) - a game no store file on this PC describes keeps what it had, which for most
/// is nothing.
/// </summary>
/// <remarks>
/// <para>
/// It is the store's word and one of three pieces of evidence the page shows; it is never FrameLedger's verdict.
/// </para>
/// <para>
/// <b>A viewer reads nothing of this PC</b> (D52): the copy it shows came from another PC, whose stores are not here.
/// It shows what the copy's rows hold.
/// </para>
/// </remarks>
public sealed class StoreControllerSupport
{
    private const string _steam = "steam";

    private readonly StoreRecordsReader _stores;
    private readonly IGameRepository _games;
    private readonly UiMode _mode;
    private readonly Func<string?> _appInfoFile;
    private readonly TimeProvider _clock;

    /// <summary>A reader over the stores' records and the Steam app's own file.</summary>
    /// <param name="stores">The stores' records of what they installed.</param>
    /// <param name="games">Where the fact is written.</param>
    /// <param name="mode">A viewer reads nothing of this PC.</param>
    /// <param name="appInfoFile">Where Steam's file is; null is where the Steam app on this PC keeps it.</param>
    /// <param name="clock">When the fact was read.</param>
    public StoreControllerSupport(StoreRecordsReader stores, IGameRepository games, UiMode mode, Func<string?>? appInfoFile = null, TimeProvider? clock = null)
    {
        _stores = stores ?? throw new ArgumentNullException(nameof(stores));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _mode = mode ?? throw new ArgumentNullException(nameof(mode));
        _appInfoFile = appInfoFile ?? SteamAppInfo.InstalledFile;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>The game's row with the store's word as it stands now: the row given when nothing could be read or nothing changed.</summary>
    public async Task<GameRow> RefreshAsync(GameRow game, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (_mode.IsViewer)
        {
            return game;
        }

        try
        {
            IReadOnlyList<StoreGame> records = await _stores.ReadAsync("controller support", [_steam], ct).ConfigureAwait(false);
            if (StoreRecords.Owning(game.Fingerprint.ExePath, game.StoreId, records) is not { Platform: _steam } record
                || !uint.TryParse(record.StoreId, NumberStyles.None, CultureInfo.InvariantCulture, out uint appId)
                || _appInfoFile() is not { } file)
            {
                return game;
            }

            SteamAppInfoResult read = await Task.Run(() => SteamAppInfo.ReadControllerSupport(file, [appId]), ct).ConfigureAwait(false);
            if (read.Problem is { } problem)
            {
                Log.Information("controller support: Steam's file was not read ({Problem})", problem);
                return game;
            }

            if (!read.Support.TryGetValue(appId, out string? support) || string.Equals(support, game.ControllerSupport, StringComparison.Ordinal))
            {
                return game;
            }

            DateTimeOffset now = _clock.GetUtcNow();
            return await _games.SetControllerSupportAsync(game.Id, support, SteamAppInfo.Source, now, ct).ConfigureAwait(false)
                ? game with { ControllerSupport = support, ControllerSupportSource = SteamAppInfo.Source, ControllerSupportAt = now }
                : game;
        }
        catch (Exception e) when (e is not OperationCanceledException and not OutOfMemoryException)
        {
            // The page opens without it: a store's file is never a reason for a game's page to fail.
            Log.Warning("controller support: {Message}", e.Message);
            return game;
        }
    }
}
