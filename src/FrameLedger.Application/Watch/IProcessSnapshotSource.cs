namespace FrameLedger.Application.Watch;

/// <summary>
/// Every process on the machine, once per poll. The adapter is <c>Infrastructure.Watch.ToolhelpProcessSnapshotSource</c>
/// (<c>CreateToolhelp32Snapshot</c>, <c>QueryFullProcessImageName</c>, <c>GetProcessTimes</c>); this is what the
/// watcher and the election are tested against.
/// </summary>
public interface IProcessSnapshotSource
{
    /// <summary>Take one snapshot. A process that cannot be opened is still listed, with <see cref="ProcessSnapshot.ImagePath"/> null.</summary>
    IReadOnlyList<ProcessSnapshot> Take();
}
