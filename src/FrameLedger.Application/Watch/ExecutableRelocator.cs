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
    private readonly Lock _logged = new();
    private readonly Dictionary<long, string> _lastLine = [];

    /// <summary>The relocator over the library, the disk and the machine's drive list.</summary>
    /// <param name="games">The library, whose rows move.</param>
    /// <param name="identity">Reads a file's fingerprint, or null when it is not there.</param>
    /// <param name="roots">The mounted volumes' roots (<c>C:\</c>, <c>H:\</c>, …); an adapter's, so a test can name its own.</param>
    /// <param name="log">The Agent's log line.</param>
    /// <param name="clock">The row's <c>updated_at</c>.</param>
    public ExecutableRelocator(IGameRepository games, IExecutableIdentitySource identity, Func<IReadOnlyList<string>> roots, Action<string> log, TimeProvider? clock = null)
    {
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _roots = roots ?? throw new ArgumentNullException(nameof(roots));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _clock = clock ?? TimeProvider.System;
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
    /// The row's executable is gone from its path: find it under another root and move the row. Null when the file is
    /// still where the row says, when no other root has it, or when more than one does.
    /// </summary>
    public async ValueTask<ExecutableFingerprint?> TryRelocateAsync(GameRow row, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (_identity.Read(row.Fingerprint.ExePath) is not null)
        {
            Forget(row.Id);
            return null;
        }

        List<ExecutableFingerprint> found = SameBytesUnder(row, Candidates(row.Fingerprint.ExePath, _roots()), ct);
        if (found.Count > 1)
        {
            LogOnce(row.Id, $"relocate: {row.Name} — {found.Count} drives hold {row.Fingerprint.ExePath[2..]} with the same size and mtime; not guessing which");
            return null;
        }

        return found.Count == 1 ? await MoveAsync(row, found[0], ct).ConfigureAwait(false) : null;
    }

    /// <summary>
    /// The watcher saw the game run from <paramref name="observedPath"/>, not the row's path (a file-name match). When the
    /// row's file is gone, the running one is the row's path with only its drive letter changed, and it is the only file
    /// there with the row's size and mtime, it is the row's executable on a drive that changed its letter: move the row, so
    /// the session is the row's and its consent applies. False otherwise — including when both files exist, which is two
    /// copies, not a move, and when the running file is in another folder, which is another game (2026-09-23).
    /// </summary>
    public async ValueTask<bool> TryAdoptAsync(GameRow row, string observedPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentException.ThrowIfNullOrWhiteSpace(observedPath);
        if (!IsDriveLetterTwin(row.Fingerprint.ExePath, observedPath)
            || _identity.Read(row.Fingerprint.ExePath) is not null
            || _identity.Read(observedPath) is not { } onDisk
            || !SameBytes(row.Fingerprint, onDisk))
        {
            return false;
        }

        // The running file's own drive is mounted by definition; the rest of the list may not know it yet.
        List<string> roots = [.. _roots(), observedPath[..3]];
        List<ExecutableFingerprint> found = SameBytesUnder(row, Candidates(row.Fingerprint.ExePath, roots), ct);
        if (found.Count != 1)
        {
            LogOnce(row.Id, $"relocate: {row.Name} — {found.Count} drives hold {row.Fingerprint.ExePath[2..]} with the same size and mtime; not guessing which");
            return false;
        }

        return await MoveAsync(row, onDisk, ct).ConfigureAwait(false) is not null;
    }

    private static bool IsDriveLetterPath(string path) =>
        path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/');

    private static bool SameBytes(ExecutableFingerprint stored, ExecutableFingerprint onDisk) =>
        stored.SizeBytes == onDisk.SizeBytes && stored.MtimeUnixMs == onDisk.MtimeUnixMs;

    private List<ExecutableFingerprint> SameBytesUnder(GameRow row, IReadOnlyList<string> candidates, CancellationToken ct)
    {
        List<ExecutableFingerprint> found = [];
        foreach (string candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();
            if (_identity.Read(_identity.Normalise(candidate)) is { } onDisk && SameBytes(row.Fingerprint, onDisk))
            {
                found.Add(onDisk);
            }
        }

        return found;
    }

    private async ValueTask<ExecutableFingerprint?> MoveAsync(GameRow row, ExecutableFingerprint moved, CancellationToken ct)
    {
        bool done = await _games.RelocateExecutableAsync(row.Id, moved, _clock.GetUtcNow(), ct).ConfigureAwait(false);
        if (done)
        {
            Forget(row.Id);
            _log($"relocate: {row.Name} — executable moved {row.Fingerprint.ExePath} → {moved.ExePath} (same size and mtime); the row follows it, consent and block kept");
            return moved;
        }

        LogOnce(row.Id, $"relocate: {row.Name} — {moved.ExePath} has the row's size and mtime but the row could not be moved (another row owns that path, or the row changed underneath)");
        return null;
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
