namespace FrameLedger.Application.Watch;

/// <summary>
/// One process as the 1 Hz watcher saw it (P2 PR-F, <c>04_CAPTURE</c> §Process watcher).
/// </summary>
/// <param name="Pid">The process id.</param>
/// <param name="ParentPid">The parent's id as the kernel reports it — a pid, which can have been reused since.</param>
/// <param name="ImageName">The executable's file name from the snapshot itself, always readable.</param>
/// <param name="ImagePath">The full, normalised image path; null when the process could not be opened (another user's, protected, gone).</param>
/// <param name="StartedAt">The process's creation time; null when it could not be read. Together with <paramref name="ParentPid"/> this is what defeats pid reuse.</param>
public readonly record struct ProcessSnapshot(int Pid, int ParentPid, string ImageName, string? ImagePath, DateTimeOffset? StartedAt);
