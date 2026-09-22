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
/// <b>Three callers, one rule.</b> The 15 s sweep (a missing executable), the watcher (a process running from a path
/// that is not the row's while the row's is gone) and the enable-hooking command (the same, when the user clicks). No
/// caller opens a process; this reads files.
/// </para>
/// </remarks>
public sealed class ExecutableRelocator
{
    private readonly IGameRepository _games;
    private readonly IExecutableIdentitySource _identity;
    private readonly Func<IReadOnlyList<string>> _roots;
    private readonly Action<string> _log;
    private readonly TimeProvider _clock;

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
        if (exePath.Length < 3 || exePath[1] != ':' || (exePath[2] != '\\' && exePath[2] != '/'))
        {
            return [];
        }

        string tail = exePath[3..];
        List<string> candidates = [];
        foreach (string root in roots)
        {
            if (root.Length < 2 || root[1] != ':' || char.ToUpperInvariant(root[0]) == char.ToUpperInvariant(exePath[0]))
            {
                continue;
            }

            candidates.Add(root.TrimEnd('\\', '/') + "\\" + tail);
        }

        return candidates;
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
            return null;
        }

        List<ExecutableFingerprint> found = [];
        foreach (string candidate in Candidates(row.Fingerprint.ExePath, _roots()))
        {
            ct.ThrowIfCancellationRequested();
            if (_identity.Read(_identity.Normalise(candidate)) is { } onDisk && SameBytes(row.Fingerprint, onDisk))
            {
                found.Add(onDisk);
            }
        }

        if (found.Count > 1)
        {
            _log($"relocate: {row.Name} — {found.Count} drives hold {row.Fingerprint.ExePath[2..]} with the same size and mtime; not guessing which");
            return null;
        }

        return found.Count == 1 ? await MoveAsync(row, found[0], ct).ConfigureAwait(false) : null;
    }

    /// <summary>
    /// The watcher saw the game run from <paramref name="observedPath"/>, not the row's path (a file-name match). When
    /// the row's file is gone and the running one has the row's size and mtime, it is the row's executable on a drive
    /// that changed its letter: move the row, so the session is the row's and its consent applies. False otherwise —
    /// including when both files exist, which is two copies, not a move.
    /// </summary>
    public async ValueTask<bool> TryAdoptAsync(GameRow row, string observedPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentException.ThrowIfNullOrWhiteSpace(observedPath);
        if (_identity.Read(row.Fingerprint.ExePath) is not null || _identity.Read(observedPath) is not { } onDisk || !SameBytes(row.Fingerprint, onDisk))
        {
            return false;
        }

        return await MoveAsync(row, onDisk, ct).ConfigureAwait(false) is not null;
    }

    private static bool SameBytes(ExecutableFingerprint stored, ExecutableFingerprint onDisk) =>
        stored.SizeBytes == onDisk.SizeBytes && stored.MtimeUnixMs == onDisk.MtimeUnixMs;

    private async ValueTask<ExecutableFingerprint?> MoveAsync(GameRow row, ExecutableFingerprint moved, CancellationToken ct)
    {
        bool done = await _games.RelocateExecutableAsync(row.Id, moved, _clock.GetUtcNow(), ct).ConfigureAwait(false);
        _log(done
            ? $"relocate: {row.Name} — executable moved {row.Fingerprint.ExePath} → {moved.ExePath} (same size and mtime); the row follows it, consent and block kept"
            : $"relocate: {row.Name} — {moved.ExePath} has the row's size and mtime but the row could not be moved (another row owns that path, or the row changed underneath)");
        return done ? moved : null;
    }
}
