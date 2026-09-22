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
    /// Whether any process carries the executable's FILE NAME — readable or not (2026-09-22). The Tier-2 hold's clock
    /// for a target the loop never opened: a game whose hooking is off is never opened at all, and a process the
    /// resolver could not read cannot be pinned. A name is not an identity, so nothing is ever injected on the strength
    /// of this answer; it only decides how long duration and telemetry accrue.
    /// </summary>
    bool IsRunning(string normalisedExePath);
}
