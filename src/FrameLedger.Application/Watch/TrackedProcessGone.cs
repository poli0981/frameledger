using FrameLedger.Application.Persistence;

namespace FrameLedger.Application.Watch;

/// <summary>A process the watcher had matched to a tracked game is no longer in the snapshot.</summary>
/// <param name="Pid">The process that is gone.</param>
/// <param name="Game">The row it had matched.</param>
public sealed record TrackedProcessGone(int Pid, GameRow Game) : WatchEvent(Pid);
