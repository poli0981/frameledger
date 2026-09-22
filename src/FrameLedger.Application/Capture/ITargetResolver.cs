namespace FrameLedger.Application.Capture;

/// <summary>How the host finds the process to capture. There is no pid argument anywhere.</summary>
public interface ITargetResolver
{
    /// <summary>
    /// The single running process whose image is <paramref name="normalisedExePath"/>.
    /// </summary>
    /// <returns>The pid, or null with <paramref name="reason"/> set.</returns>
    int? Resolve(string normalisedExePath, out SessionEndReason reason);

    /// <summary>
    /// Whether the executable is running (2026-09-22). The Tier-2 hold's clock for a target the loop never opened: a game
    /// whose hooking is off is never opened at all, and a process the resolver could not read cannot be pinned. Where a
    /// watcher runs it answers from the watcher's snapshot by the IMAGE PATH (2026-09-23 — by file name, another game's
    /// <c>Game.exe</c> kept an RPG Maker game's hold open), a process whose path could not be read still counting by its
    /// name; the console verbs, where nothing polls, keep the name. Nothing is ever injected on the strength of this answer;
    /// it only decides how long duration and telemetry accrue.
    /// </summary>
    bool IsRunning(string normalisedExePath);
}
