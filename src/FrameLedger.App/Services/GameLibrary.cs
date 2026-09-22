using System.IO;
using FrameLedger.Application.Import;
using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Consent;
using FrameLedger.Infrastructure.Import;
using FrameLedger.Infrastructure.Io;

namespace FrameLedger.App.Services;

/// <summary>
/// The library as the pages read and write it (<c>06_DATA_MODEL</c> §Writer ownership: the UI's part of
/// <c>games</c>, and <c>session_annotations</c>). Adding a game is one <c>games</c> row with hooking OFF
/// (CLAUDE.md rule 1) — the Agent's watcher reads the same table each tick, so nothing is sent over the pipe to
/// make it watch. Removing is FR-1.4's two shapes. Nothing here touches a hook-state column.
/// </summary>
public sealed class GameLibrary
{
    private readonly IGameRepository _games;
    private readonly ISessionRepository _sessions;
    private readonly ISessionAnnotationRepository _annotations;
    private readonly TimeProvider _clock;

    public GameLibrary(IGameRepository games, ISessionRepository sessions, ISessionAnnotationRepository annotations, TimeProvider? clock = null)
    {
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _annotations = annotations ?? throw new ArgumentNullException(nameof(annotations));
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Every game in the library with its aggregate, by name.</summary>
    public async Task<IReadOnlyList<GameCard>> ListCardsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<GameRow> rows = await _games.ListAsync(ct).ConfigureAwait(false);
        Dictionary<long, GameSessionSummary> summaries = (await _sessions.SummariseByGameAsync(ct).ConfigureAwait(false)).ToDictionary(static s => s.GameId);
        return [.. rows.Select(r => new GameCard(r, summaries.GetValueOrDefault(r.Id)))];
    }

    /// <summary>The detail page's load; null when the game does not exist.</summary>
    public async Task<GameDetail?> LoadAsync(long gameId, int sessionLimit = 500, CancellationToken ct = default)
    {
        GameRow? row = await _games.FindByIdAsync(gameId, ct).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        IReadOnlyList<SessionRow> sessions = await _sessions.ListByGameAsync(gameId, sessionLimit, ct).ConfigureAwait(false);
        IReadOnlyList<SessionAnnotation> annotations = await _annotations.ListByGameAsync(gameId, ct).ConfigureAwait(false);
        GameSessionSummary? summary = (await _sessions.SummariseByGameAsync(ct).ConfigureAwait(false)).FirstOrDefault(s => s.GameId == gameId);
        return new GameDetail(row, sessions, annotations.ToDictionary(static a => a.SessionId), summary);
    }

    /// <summary>FR-1.1: the executable becomes a <c>games</c> row, hooking off; null when the file cannot be read.</summary>
    public async Task<GameRow?> AddAsync(string exePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        string normalised = ExecutableIdentity.Normalise(exePath);
        ExecutableFingerprint? fingerprint = ExecutableIdentity.Read(normalised);
        if (fingerprint is null)
        {
            return null;
        }

        // A generic executable (Game.exe) is named from the game's own title or its folder, not "Game" (2026-09-23).
        string name = GameTitleGuess.Guess(normalised, () => RpgMakerTitle.TryRead(normalised));
        return await _games.EnsureAsync(fingerprint.Value, name, ct).ConfigureAwait(false);
    }

    /// <summary>Whether the row's executable is where the row says (2026-09-22): the page's "not found" line, nothing else.</summary>
    public static bool ExecutableExists(string normalisedExePath) => ExecutableIdentity.Read(normalisedExePath) is not null;

    /// <summary>
    /// Points the row at another executable; null when the file cannot be read, false when another row owns that path.
    /// The caller revokes consent over the pipe FIRST, as removal does: the table write only downgrades.
    /// </summary>
    public async Task<bool?> ChangeExecutableAsync(long gameId, string exePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        string normalised = ExecutableIdentity.Normalise(exePath);
        ExecutableFingerprint? fingerprint = ExecutableIdentity.Read(normalised);
        if (fingerprint is null)
        {
            return null;
        }

        return await _games.ChangeExecutableAsync(gameId, fingerprint.Value, _clock.GetUtcNow(), ct).ConfigureAwait(false);
    }

    public Task<bool> RemoveAsync(long gameId, bool keepSessions, CancellationToken ct = default) => _games.RemoveAsync(gameId, keepSessions, ct).AsTask();

    public Task<bool> UpdateMetadataAsync(long gameId, GameMetadata metadata, CancellationToken ct = default) => _games.UpdateMetadataAsync(gameId, metadata, ct).AsTask();

    /// <summary>The Dashboard's recent list: the last <paramref name="count"/> sessions across games, with their game names.</summary>
    public async Task<IReadOnlyList<RecentSession>> RecentAsync(int count, CancellationToken ct = default)
    {
        IReadOnlyList<SessionRow> rows = await _sessions.ListRecentAsync(count, ct).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return [];
        }

        var names = new Dictionary<long, string>();
        foreach (long gameId in rows.Select(static r => r.GameId).Distinct())
        {
            GameRow? game = await _games.FindByIdAsync(gameId, ct).ConfigureAwait(false);
            names[gameId] = game?.Name ?? Strings.Common_NotAvailable;
        }

        return [.. rows.Select(r => new RecentSession(r, names[r.GameId]))];
    }

    public async Task<LibraryTotals> TotalsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<GameRow> rows = await _games.ListAsync(ct).ConfigureAwait(false);
        IReadOnlyList<GameSessionSummary> summaries = await _sessions.SummariseByGameAsync(ct).ConfigureAwait(false);
        long thisWeek = await _sessions.CountSinceAsync(_clock.GetUtcNow().AddDays(-7), ct).ConfigureAwait(false);
        return new LibraryTotals(rows.Count, summaries.Sum(static s => s.TotalSeconds), thisWeek);
    }
}
