using FrameLedger.Application.Persistence;
using FrameLedger.Domain.Consent;

namespace FrameLedger.Application.Watch;

/// <summary>
/// A drive that changed its letter is the same executable (2026-09-22; <c>19_SAFETY</c> §A moved drive is the same
/// executable). Every row is keyed on its executable's path, so when the owner's external drive came back as <c>H:</c>
/// instead of <c>D:</c> all 46 games read "executable unreadable" for as long as it stayed there, and a game launched
/// from <c>H:</c> matched its row by file name only and ran as a stranger. This finds the same file under another
/// volume root — same size, same mtime, exactly one candidate — and moves the row to it with everything kept.
/// </summary>
/// <remarks>
/// <para>
/// <b>What "the same file" means here.</b> The consent fingerprint is path + size + mtime; size and mtime are the
/// bytes' identity and the path is where they live. A candidate must match both to the millisecond, and the
/// repository's write requires the same again, so nothing here can point a consent at a different binary. Two
/// candidates (two drives with the file) is an ambiguity, and ambiguity is a refusal, as it is in the resolver.
/// </para>
/// <para>
/// <b>Only a drive letter may differ (2026-09-23).</b> The watcher's adopt used to accept ANY running path with the
/// row's size and mtime — and an RPG Maker MV game's <c>Game.exe</c> is the same NW.js stub in every game, byte for
/// byte, with the mtime an unzip keeps: a hooking-on row whose drive was unplugged could have been moved, consent and
/// all, onto another game. Every caller now asks for the same thing: the row's path with only its letter changed
/// (<see cref="IsDriveLetterTwin"/>), and exactly one such file with the row's bytes.
/// </para>
/// <para>
/// <b>Two more answers since the same day (HANDOFF D26, D27).</b> When the only file at the row's path under another
/// letter has OTHER bytes — the game was updated while the drive had another letter — the row follows it the way
/// <i>Change executable</i> does: hooking off, consent cleared, the block kept (D27). And when the file the row
/// would move to already has an entry of its own — a twin made while the letter was different — and it is the same
/// bytes the row recorded, the two entries become one (<see cref="IGameMerge"/>, D26): the entry made first stays,
/// the other's sessions join it, a block or auto-disable either carried is kept, and no consent moves between them.
/// A merge waits while a session of either entry runs or waits to be recovered (<c>busy</c>).
/// </para>
/// <para>
/// <b>Three callers, one rule.</b> The 15 s sweep (a missing executable), the watcher (a process running from a path
/// that is not the row's while the row's is gone) and the enable-hooking command (the same, when the user clicks). No
/// caller opens a process; this reads files. A refusal is logged once per row until it changes — the owner's log
/// carried the same "could not be moved" line every 15 s, 5,675 times on 2026-09-22.
/// </para>
/// </remarks>
public sealed class ExecutableRelocator
{
    private readonly IGameRepository _games;
    private readonly IExecutableIdentitySource _identity;
    private readonly Func<IReadOnlyList<string>> _roots;
    private readonly Action<string> _log;
    private readonly TimeProvider _clock;
    private readonly IGameMerge? _merge;
    private readonly Func<long, bool> _busy;
    private readonly Lock _logged = new();
    private readonly Dictionary<long, string> _lastLine = [];

    /// <summary>The relocator over the library, the disk and the machine's drive list.</summary>
    /// <param name="games">The library, whose rows move.</param>
    /// <param name="identity">Reads a file's fingerprint, or null when it is not there.</param>
    /// <param name="roots">The mounted volumes' roots (<c>C:\</c>, <c>H:\</c>, …); an adapter's, so a test can name its own.</param>
    /// <param name="log">The Agent's log line.</param>
    /// <param name="clock">The row's <c>updated_at</c>.</param>
    /// <param name="merge">Folds a twin entry into its other half (D26); null never merges.</param>
    /// <param name="busy">Whether a session of the entry runs or waits to be recovered; a merge waits for it. Null is never busy.</param>
    public ExecutableRelocator(IGameRepository games, IExecutableIdentitySource identity, Func<IReadOnlyList<string>> roots, Action<string> log, TimeProvider? clock = null,
        IGameMerge? merge = null, Func<long, bool>? busy = null)
    {
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _roots = roots ?? throw new ArgumentNullException(nameof(roots));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _clock = clock ?? TimeProvider.System;
        _merge = merge;
        _busy = busy ?? (static _ => false);
    }

    /// <summary>The same path under each other drive-letter root; empty for a path that is not a drive-letter path (UNC).</summary>
    public static IReadOnlyList<string> Candidates(string exePath, IReadOnlyList<string> roots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        ArgumentNullException.ThrowIfNull(roots);
        if (!IsDriveLetterPath(exePath))
        {
            return [];
        }

        string tail = exePath[3..];
        List<string> candidates = [];
        HashSet<char> seen = [char.ToUpperInvariant(exePath[0])];
        foreach (string root in roots)
        {
            if (root.Length < 2 || root[1] != ':' || !seen.Add(char.ToUpperInvariant(root[0])))
            {
                continue;
            }

            candidates.Add(root.TrimEnd('\\', '/') + "\\" + tail);
        }

        return candidates;
    }

    /// <summary>
    /// Whether <paramref name="a"/> and <paramref name="b"/> are the same path on two different drive letters
    /// (2026-09-23) — the only difference a moved drive can make. Case-insensitive, as NTFS is; UNC paths never are.
    /// </summary>
    public static bool IsDriveLetterTwin(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return IsDriveLetterPath(a) && IsDriveLetterPath(b)
            && char.ToUpperInvariant(a[0]) != char.ToUpperInvariant(b[0])
            && string.Equals(a[3..], b[3..], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The row's executable is gone from its path: find it under another root and move the row — or follow it with
    /// hooking off when its bytes changed (D27), or merge the row into the entry that already holds that path (D26).
    /// The row's executable as it now is, when the row still exists and moved; null when the file is still where the row
    /// says, when no other root has it, when more than one does, and when the row was merged into the other entry.
    /// </summary>
    public async ValueTask<ExecutableFingerprint?> TryRelocateAsync(GameRow row, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (_identity.Read(row.Fingerprint.ExePath) is not null)
        {
            Forget(row.Id);
            return null;
        }

        List<ExecutableFingerprint> onDisk = Existing(Candidates(row.Fingerprint.ExePath, _roots()), ct);
        List<ExecutableFingerprint> same = [.. onDisk.Where(f => SameBytes(row.Fingerprint, f))];
        if (same.Count == 1)
        {
            return await MoveAsync(row, same[0], ct).ConfigureAwait(false);
        }

        if (same.Count > 1 || onDisk.Count > 1)
        {
            LogAmbiguous(row, onDisk.Count, sameBytes: same.Count > 1);
            return null;
        }

        return onDisk.Count == 1 ? await FollowChangedAsync(row, onDisk[0], ct).ConfigureAwait(false) : null;
    }

    /// <summary>
    /// The watcher saw the game run from <paramref name="observedPath"/>, not the row's path (a file-name match). When the
    /// row's file is gone and the running one is the row's path with only its drive letter changed, it is the row's
    /// executable on a drive that changed its letter: the row moves to it — with everything kept when it is the only file
    /// there with the row's size and mtime, or with hooking off when it is the only file there at all and its bytes
    /// changed (D27) — so the session is the row's. False otherwise — including when both files exist, which is two
    /// copies, not a move, and when the running file is in another folder, which is another game (2026-09-23).
    /// </summary>
    public async ValueTask<bool> TryAdoptAsync(GameRow row, string observedPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentException.ThrowIfNullOrWhiteSpace(observedPath);
        if (!IsDriveLetterTwin(row.Fingerprint.ExePath, observedPath)
            || _identity.Read(row.Fingerprint.ExePath) is not null
            || _identity.Read(observedPath) is not { } running)
        {
            return false;
        }

        // The running file's own drive is mounted by definition; the rest of the list may not know it yet.
        List<string> roots = [.. _roots(), observedPath[..3]];
        List<ExecutableFingerprint> onDisk = Existing(Candidates(row.Fingerprint.ExePath, roots), ct);
        int same = onDisk.Count(f => SameBytes(row.Fingerprint, f));
        if (SameBytes(row.Fingerprint, running))
        {
            if (same != 1)
            {
                LogAmbiguous(row, onDisk.Count, sameBytes: true);
                return false;
            }

            return await MoveAsync(row, running, ct).ConfigureAwait(false) is not null;
        }

        if (same > 0 || onDisk.Count != 1)
        {
            LogAmbiguous(row, onDisk.Count, sameBytes: same > 1);
            return false;
        }

        return await FollowChangedAsync(row, running, ct).ConfigureAwait(false) is not null;
    }

    private static bool IsDriveLetterPath(string path) =>
        path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/');

    private static bool SameBytes(ExecutableFingerprint stored, ExecutableFingerprint onDisk) =>
        stored.SizeBytes == onDisk.SizeBytes && stored.MtimeUnixMs == onDisk.MtimeUnixMs;

    private List<ExecutableFingerprint> Existing(IReadOnlyList<string> candidates, CancellationToken ct)
    {
        List<ExecutableFingerprint> found = [];
        foreach (string candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();
            if (_identity.Read(_identity.Normalise(candidate)) is { } onDisk)
            {
                found.Add(onDisk);
            }
        }

        return found;
    }

    private void LogAmbiguous(GameRow row, int drives, bool sameBytes) =>
        LogOnce(row.Id, $"relocate: {row.Name} — {drives} drives hold {row.Fingerprint.ExePath[2..]}{(sameBytes ? " with the same size and mtime" : string.Empty)}; not guessing which");

    private async ValueTask<ExecutableFingerprint?> MoveAsync(GameRow row, ExecutableFingerprint moved, CancellationToken ct)
    {
        bool done = await _games.RelocateExecutableAsync(row.Id, moved, _clock.GetUtcNow(), ct).ConfigureAwait(false);
        if (done)
        {
            Forget(row.Id);
            _log($"relocate: {row.Name} — executable moved {row.Fingerprint.ExePath} → {moved.ExePath} (same size and mtime); the row follows it, consent and block kept");
            return moved;
        }

        // Refused: another row owns that path, or this one changed underneath. The first, when that row is in the library,
        // is a twin made while the letter was different (D26).
        if (_merge is not null && await _games.FindAsync(moved.ExePath, ct).ConfigureAwait(false) is { InLibrary: true } owner && owner.Id != row.Id)
        {
            return await MergeAsync(row, owner, moved, ct).ConfigureAwait(false);
        }

        LogOnce(row.Id, $"relocate: {row.Name} — {moved.ExePath} has the row's size and mtime but the row could not be moved (another row owns that path, or the row changed underneath)");
        return null;
    }

    /// <summary>
    /// D27 (owner, 2026-09-23): the row's file is gone and the only file at its path under another letter has other bytes
    /// — the game was updated while the drive had another letter. The row follows it as <i>Change executable</i> does
    /// (<see cref="IGameRepository.ChangeExecutableAsync"/>): hooking off, consent cleared, the block kept — a downgrade by
    /// construction, never a grant.
    /// </summary>
    private async ValueTask<ExecutableFingerprint?> FollowChangedAsync(GameRow row, ExecutableFingerprint changed, CancellationToken ct)
    {
        if (await _games.ChangeExecutableAsync(row.Id, changed, _clock.GetUtcNow(), ct).ConfigureAwait(false))
        {
            Forget(row.Id);
            _log($"relocate: {row.Name} — executable moved {row.Fingerprint.ExePath} → {changed.ExePath} and changed since the entry recorded it "
                 + "(other size or mtime); the entry follows it with hooking OFF, consent cleared, any block kept — turn hooking on again to measure it");
            return changed;
        }

        // Not merged: other bytes are not proof that the entry holding that path is this one's game (D26 merges the same bytes only).
        LogOnce(row.Id, await _games.FindAsync(changed.ExePath, ct).ConfigureAwait(false) is { } owner && owner.Id != row.Id
            ? $"relocate: {row.Name} — its executable is now {changed.ExePath}, which the entry '{owner.Name}' already holds, and the file there is not "
              + "the one this entry recorded, so the two are not merged; remove the entry you do not want"
            : $"relocate: {row.Name} — {changed.ExePath} could not be followed (the row changed underneath)");
        return null;
    }

    /// <summary>
    /// D26 (owner, 2026-09-23): <paramref name="stale"/>'s file is gone and <paramref name="owner"/> is the entry at the
    /// same file under another letter, with the bytes <paramref name="stale"/> recorded: one executable, two entries. They
    /// become one unless a session of either is running or waiting to be recovered.
    /// </summary>
    private async ValueTask<ExecutableFingerprint?> MergeAsync(GameRow stale, GameRow owner, ExecutableFingerprint target, CancellationToken ct)
    {
        if (_busy(stale.Id) || _busy(owner.Id))
        {
            LogOnce(stale.Id, $"relocate: {stale.Name} and {owner.Name} are one executable ({target.ExePath}) under two drive letters; "
                              + "they are merged once no session of either is running or waiting to be recovered");
            return null;
        }

        GameMergePlan plan = GameMergePlan.For(stale, owner, target);
        if (await _merge!.MergeAsync(plan, ct).ConfigureAwait(false) is not { } sessions)
        {
            LogOnce(stale.Id, $"relocate: {stale.Name} — not merged with {owner.Name} (one of the two entries changed underneath); the next pass looks again");
            return null;
        }

        Forget(stale.Id);
        Forget(owner.Id);
        (GameRow keep, GameRow drop) = plan.SurvivorId == stale.Id ? (stale, owner) : (owner, stale);
        _log($"relocate: merged — '{drop.Name}' ({drop.Fingerprint.ExePath}) and '{keep.Name}' ({keep.Fingerprint.ExePath}) are one executable under two "
             + $"drive letters; '{keep.Name}' stays, at {target.ExePath}, with {sessions} session(s) moved to it"
             + (drop.HookBlockedReason is not null && keep.HookBlockedReason is null ? ", and the other entry's block" : string.Empty));
        return plan.SurvivorMoves ? target : null;
    }

    /// <summary>The same refusal about the same row is said once, until something about it changes.</summary>
    private void LogOnce(long rowId, string line)
    {
        lock (_logged)
        {
            if (_lastLine.TryGetValue(rowId, out string? last) && string.Equals(last, line, StringComparison.Ordinal))
            {
                return;
            }

            _lastLine[rowId] = line;
        }

        _log(line);
    }

    private void Forget(long rowId)
    {
        lock (_logged)
        {
            _lastLine.Remove(rowId);
        }
    }
}
