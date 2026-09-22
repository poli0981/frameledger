using FrameLedger.Application.Persistence;

namespace FrameLedger.Application.Watch;

/// <summary>A process of a tracked game appeared since the last poll.</summary>
/// <param name="Pid">The new process.</param>
/// <param name="Game">The <c>games</c> row it matched.</param>
/// <param name="ImagePath">The image path the process actually runs from, normalised — the path a session is keyed on.</param>
/// <param name="StalePath">
/// True when the match was by file name only, and <paramref name="ImagePath"/> differs from the row's. Since 2026-09-23
/// it starts nothing by itself (HANDOFF D25): it is the row's executable only when the relocator moves the row to it (a
/// drive that changed its letter), and otherwise a process that is not in the library.
/// </param>
public sealed record TrackedProcessAppeared(int Pid, GameRow Game, string ImagePath, bool StalePath) : WatchEvent(Pid);
