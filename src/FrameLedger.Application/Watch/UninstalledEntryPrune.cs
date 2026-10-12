// SPDX-License-Identifier: GPL-3.0-only
// Copyright (C) 2026 poli0981 - additional terms under GPLv3 section 7: see NOTICE

using System.Globalization;
using FrameLedger.Application.Ipc;
using FrameLedger.Application.Persistence;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.Application.Watch;

/// <summary>
/// Removes a library entry whose game is no longer installed and that has no session (beta.19, owner decision D55, amending
/// D41's "nothing is removed"). Asked by <c>DetectionSweep</c> about every entry whose executable stayed unreadable after the
/// relocator looked for it on the other drives.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every condition, or nothing.</b> The file is <see cref="ExecutablePresence.FileGone"/> — its drive connected, its folder
/// or the file gone from a folder that is still there, not a refused listing; it has been so at sweeps at least
/// <see cref="Grace"/> apart, at the same path (an update that replaces the executable is not an uninstall); the user's switch
/// (<c>retention.remove_uninstalled_games</c>, on by default) is on; no session of it is being recorded or waits in a
/// <c>.partial</c> to be recovered; and it carries no anti-cheat block, exception or failed-trial mark — rule 2's block is
/// never cleared, and a re-added entry would start without it. The delete itself (<see cref="IGamePrune"/>) checks the stored
/// conditions again in one statement, with "no session stored" among them, so a session finalized meanwhile wins.
/// </para>
/// <para>
/// <b>Counted in memory.</b> When the file was first seen gone is this object's, not the ledger's: an Agent restart starts
/// the ten minutes again, which only delays a removal. An entry the delete refused (it has sessions) is asked again at that
/// path only after <see cref="RetryAfter"/> — its sessions may since have been deleted (FR-1.7), and an answer that does not
/// change is not worth a statement every 15 seconds.
/// </para>
/// </remarks>
public sealed class UninstalledEntryPrune
{
    /// <summary>How long a file must have been gone — across sweeps — before its entry goes: longer than a store takes to replace an executable.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(10);

    /// <summary>How long an entry the delete refused waits before it is asked again.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromHours(1);

    private readonly IGamePrune _prune;
    private readonly IExecutablePresence _presence;
    private readonly Func<CancellationToken, ValueTask<bool>> _enabled;
    private readonly Func<long, bool> _busy;
    private readonly Action<string> _log;
    private readonly IIpcEventPublisher? _pipe;
    private readonly TimeProvider _clock;
    private readonly Dictionary<long, (string Path, DateTimeOffset Since)> _goneSince = [];
    private readonly Dictionary<(long Id, string Path), DateTimeOffset> _kept = [];

    /// <summary>The prune over its writer, the file system's answer, the user's switch and the recorder's state.</summary>
    /// <param name="prune">The one conditional delete.</param>
    /// <param name="presence">Why an executable did not read.</param>
    /// <param name="enabled">The user's switch, read at every sweep.</param>
    /// <param name="busy">True while a session of the entry is recorded or waits to be recovered.</param>
    /// <param name="log">The Agent's log line.</param>
    /// <param name="pipe">Where the App hears of a removal; null tells nobody.</param>
    /// <param name="clock">The time the grace is counted in.</param>
    public UninstalledEntryPrune(IGamePrune prune, IExecutablePresence presence, Func<CancellationToken, ValueTask<bool>> enabled, Func<long, bool> busy,
        Action<string> log, IIpcEventPublisher? pipe = null, TimeProvider? clock = null)
    {
        _prune = prune ?? throw new ArgumentNullException(nameof(prune));
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        _enabled = enabled ?? throw new ArgumentNullException(nameof(enabled));
        _busy = busy ?? throw new ArgumentNullException(nameof(busy));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _pipe = pipe;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>The entry's executable read: whatever was counted against it is forgotten.</summary>
    public void Present(long gameId) => _goneSince.Remove(gameId);

    /// <summary>
    /// The entry's executable did not read and was not relocated: is the game uninstalled, and may its entry go? True when the
    /// entry was removed.
    /// </summary>
    public async ValueTask<bool> ConsiderAsync(GameRow game, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        string path = game.Fingerprint.ExePath;
        if (_presence.Probe(path) != ExecutablePresence.FileGone)
        {
            _goneSince.Remove(game.Id);
            return false;
        }

        DateTimeOffset now = _clock.GetUtcNow();
        if (!_goneSince.TryGetValue(game.Id, out (string Path, DateTimeOffset Since) seen) || !string.Equals(seen.Path, path, StringComparison.OrdinalIgnoreCase))
        {
            _goneSince[game.Id] = (path, now);
            return false;
        }

        if (now - seen.Since < Grace || (_kept.TryGetValue((game.Id, path), out DateTimeOffset refused) && now - refused < RetryAfter)
            || CarriesAntiCheatState(game) || _busy(game.Id)
            || !await _enabled(ct).ConfigureAwait(false))
        {
            return false;
        }

        if (!await _prune.RemoveUninstalledAsync(game.Id, path, ct).ConfigureAwait(false))
        {
            _kept[(game.Id, path)] = now;
            return false;
        }

        _goneSince.Remove(game.Id);
        _log(string.Create(CultureInfo.InvariantCulture,
            $"library: removed '{game.Name}' — {path} has been gone since {seen.Since:u} and the entry had no session (D55; Settings ▸ Data turns this off)"));
        _pipe?.Publish(IpcMessageType.GameRemoved, new GameRemovedEvent(game.Id, game.Name, path, GameRemovedEvent.Uninstalled));
        return true;
    }

    /// <summary>A block, an exception or a failed trial: what rule 2 and D38 keep for good, and a removal would drop.</summary>
    private static bool CarriesAntiCheatState(GameRow game) =>
        game.BlockedByGuard || game.AcException.GrantedAt is not null || game.AcException.TrialFailed;
}
