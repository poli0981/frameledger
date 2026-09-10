using FrameLedger.Application.Persistence;

namespace FrameLedger.Application.Watch;

/// <summary>A process of a tracked game appeared since the last poll.</summary>
/// <param name="Pid">The new process.</param>
/// <param name="Game">The <c>games</c> row it matched.</param>
/// <param name="ImagePath">The image path the process actually runs from, normalised — the path a session is keyed on.</param>
/// <param name="StalePath">
/// True when the match was by file name only: the game moved since it was added, and <paramref name="ImagePath"/>
/// differs from the row's. The session runs against the real path, so consent given for the old one does not
/// apply until it is granted again (<c>04_CAPTURE</c> §Process watcher's "stale-path warning badge").
/// </param>
public sealed record TrackedProcessAppeared(int Pid, GameRow Game, string ImagePath, bool StalePath) : WatchEvent(Pid);
