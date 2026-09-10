namespace FrameLedger.Application.Watch;

/// <summary>What one poll of <see cref="ProcessWatcher"/> noticed about a tracked game's process.</summary>
/// <param name="Pid">The process the event is about.</param>
public abstract record WatchEvent(int Pid);
