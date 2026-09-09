using FrameLedger.Application.Persistence;

namespace FrameLedger.Application.Watch;

/// <summary>
/// The 1 Hz match of a process snapshot against the watchlist — the <c>games</c> rows — into
/// <see cref="WatchEvent"/>s (P2 PR-F, <c>04_CAPTURE</c> §Process watcher). Pure: the snapshot and the
/// watchlist come in, events come out, and the only state is which pids it has already reported.
/// </summary>
/// <remarks>
/// <para>
/// <b>Match on the normalised full path first.</b> A row's <c>exe_path</c> and the snapshot's image path are
/// both normalised the same way (<c>ExecutableIdentity.Normalise</c>), compared case-insensitively because
/// NTFS is. <b>Fall back to the file name only when it is unambiguous</b>: exactly one tracked row carries that
/// file name. Two rows sharing <c>game.exe</c> in different directories are two games, and guessing which
/// one moved is how a session lands on the wrong row. The fallback event says <c>StalePath</c>, and the
/// session it starts is keyed on the path the process actually runs from — so consent for the old path
/// does not follow the binary to a new one, which is <c>HookRequest.FromConsent</c>'s rule read from the
/// other side.
/// </para>
/// <para>
/// <b>A process that cannot be opened matches nothing.</b> Its image path is null and its name alone is
/// not an identity; the resolver's rule ("could not look" must not widen the set) applies here too.
/// </para>
/// </remarks>
public sealed class ProcessWatcher
{
    private readonly Dictionary<int, GameRow> _tracked = new();

    /// <summary>The pids currently matched to a tracked game, with the row each matched.</summary>
    public IReadOnlyDictionary<int, GameRow> Tracked => _tracked;

    /// <summary>Diff <paramref name="snapshot"/> against what was reported before; the events, in pid order.</summary>
    public IReadOnlyList<WatchEvent> Poll(IReadOnlyList<ProcessSnapshot> snapshot, IReadOnlyList<GameRow> watchlist)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(watchlist);

        Dictionary<string, GameRow> byPath = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<GameRow>> byName = new(StringComparer.OrdinalIgnoreCase);
        foreach (GameRow row in watchlist)
        {
            byPath[row.Fingerprint.ExePath] = row;
            string name = Path.GetFileName(row.Fingerprint.ExePath);
            if (!byName.TryGetValue(name, out List<GameRow>? rows))
            {
                byName[name] = rows = [];
            }

            rows.Add(row);
        }

        List<WatchEvent> events = [];
        HashSet<int> alive = [];
        foreach (ProcessSnapshot p in snapshot.OrderBy(static p => p.Pid))
        {
            alive.Add(p.Pid);
            if (_tracked.ContainsKey(p.Pid) || p.ImagePath is null)
            {
                continue;
            }

            if (byPath.TryGetValue(p.ImagePath, out GameRow? exact))
            {
                _tracked[p.Pid] = exact;
                events.Add(new TrackedProcessAppeared(p.Pid, exact, p.ImagePath, StalePath: false));
            }
            else if (byName.TryGetValue(Path.GetFileName(p.ImagePath), out List<GameRow>? candidates) && candidates.Count == 1)
            {
                _tracked[p.Pid] = candidates[0];
                events.Add(new TrackedProcessAppeared(p.Pid, candidates[0], p.ImagePath, StalePath: true));
            }
        }

        foreach ((int pid, GameRow row) in _tracked.OrderBy(static kv => kv.Key).ToArray())
        {
            if (!alive.Contains(pid))
            {
                _tracked.Remove(pid);
                events.Add(new TrackedProcessGone(pid, row));
            }
        }

        return events;
    }
}
