namespace FrameLedger.Application.Watch;

/// <summary>
/// Which process to capture when the one we started is not the one that presents (P2 PR-F, <c>04_CAPTURE</c>
/// §Process watcher: "the capture target is the descendant that actually presents").
/// </summary>
/// <remarks>
/// <para>
/// <b>Launch mode's launcher-first shape.</b> The consented executable is a launcher: it spawns the real game
/// and quits, or stays on its window while a child presents. The guard's waiting entry answers
/// <c>LaunchTargetExited</c> / <c>LaunchNoPresentationRuntime</c> for the launcher itself, with nothing
/// injected (§S1); the Agent then elects among the launcher's descendants and starts an attach-mode session
/// against the elected image — which is keyed on <i>that</i> path, so the child needs its own consent
/// record. A launcher's consent does not extend to what it spawns: the guard scans the child on its own,
/// and rule 1 (per game, never automatic) is per executable.
/// </para>
/// <para>
/// <b>The newest tracked descendant wins.</b> Level-transition relaunches and updater hops leave the
/// oldest processes dead or idle; the one that started last is the one presenting now. "Tracked" is
/// the caller's predicate — a <c>games</c> row for the path — so an election never lands on a crash
/// reporter or a helper the user never added. Pid reuse is <see cref="ProcessTree"/>'s problem, solved
/// there.
/// </para>
/// </remarks>
public static class DescendantElection
{
    /// <summary>
    /// The newest-started descendant of <paramref name="rootPid"/> whose image path satisfies
    /// <paramref name="isTracked"/>, or null when there is none. A descendant whose path could not be read
    /// is never elected — an identity we could not establish is not a target.
    /// </summary>
    public static ProcessSnapshot? Elect(IReadOnlyList<ProcessSnapshot> snapshot, int rootPid, Func<string, bool> isTracked)
    {
        ArgumentNullException.ThrowIfNull(isTracked);

        ProcessSnapshot? best = null;
        foreach (ProcessSnapshot candidate in ProcessTree.Descendants(snapshot, rootPid))
        {
            if (candidate.ImagePath is null || !isTracked(candidate.ImagePath))
            {
                continue;
            }

            if (best is null || Newer(candidate, best.Value))
            {
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>Later start wins; unknown starts lose to known ones; equal or both unknown keeps the first seen.</summary>
    private static bool Newer(ProcessSnapshot candidate, ProcessSnapshot incumbent) =>
        candidate.StartedAt is { } c && (incumbent.StartedAt is not { } i || c > i);
}
