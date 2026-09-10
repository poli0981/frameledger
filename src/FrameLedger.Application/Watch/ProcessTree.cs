namespace FrameLedger.Application.Watch;

/// <summary>
/// The process tree read off one snapshot, with the one rule that keeps it honest: <b>a child started
/// before its parent is not its child.</b> The kernel reports parent ids, and a pid is reused the moment
/// its process exits, so a launcher that quit an hour ago can be "the parent" of an unrelated process
/// that inherited its number. Creation time is the tiebreaker (<c>04_CAPTURE</c> §Process watcher, P2 PR-F).
/// </summary>
public static class ProcessTree
{
    /// <summary>
    /// Every descendant of <paramref name="rootPid"/> in <paramref name="snapshot"/>, breadth-first. A parent
    /// link whose child started before the parent is a reused pid and is not followed; a link where either
    /// time is unknown is trusted, because refusing it would hide a real child behind a right we lacked.
    /// </summary>
    public static IReadOnlyList<ProcessSnapshot> Descendants(IReadOnlyList<ProcessSnapshot> snapshot, int rootPid)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Dictionary<int, ProcessSnapshot> byPid = new();
        foreach (ProcessSnapshot p in snapshot)
        {
            byPid[p.Pid] = p;
        }

        List<ProcessSnapshot> found = [];
        HashSet<int> seen = [rootPid];
        Queue<int> frontier = new();
        frontier.Enqueue(rootPid);
        while (frontier.Count > 0)
        {
            int parentPid = frontier.Dequeue();
            DateTimeOffset? parentStart = byPid.TryGetValue(parentPid, out ProcessSnapshot parent) ? parent.StartedAt : null;
            foreach (ProcessSnapshot candidate in snapshot)
            {
                if (candidate.ParentPid != parentPid || !seen.Add(candidate.Pid))
                {
                    continue;
                }

                if (IsReusedParent(parentStart, candidate.StartedAt))
                {
                    continue;
                }

                found.Add(candidate);
                frontier.Enqueue(candidate.Pid);
            }
        }

        return found;
    }

    /// <summary>True when <paramref name="descendantPid"/> is under <paramref name="rootPid"/> by the rule above.</summary>
    public static bool IsDescendant(IReadOnlyList<ProcessSnapshot> snapshot, int rootPid, int descendantPid)
    {
        foreach (ProcessSnapshot p in Descendants(snapshot, rootPid))
        {
            if (p.Pid == descendantPid)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReusedParent(DateTimeOffset? parentStart, DateTimeOffset? childStart) =>
        parentStart is { } ps && childStart is { } cs && cs < ps;
}
