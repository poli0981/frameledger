namespace FrameLedger.Application.Watch;

/// <summary>
/// The watcher's most recent process snapshot, shared (2026-09-23). A Tier-2 hold asks "is this game still running"
/// of the same 1 Hz list the watcher matched it in, by the image path — so a hooking-off game is timed by its own
/// executable and not by any process that shares its file name (the owner's two "Flower in Us" sessions were held by
/// every <c>Game.exe</c> on the machine), and still no process is opened for the hold.
/// </summary>
public sealed class LatestProcessSnapshot
{
    private IReadOnlyList<ProcessSnapshot>? _current;

    /// <summary>Null until the watcher's first poll; the console verbs never poll, and keep the name check.</summary>
    public IReadOnlyList<ProcessSnapshot>? Current => Volatile.Read(ref _current);

    /// <summary>The watcher's poll: the list it just matched against.</summary>
    public void Publish(IReadOnlyList<ProcessSnapshot> snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Volatile.Write(ref _current, snapshot);
    }

    /// <summary>
    /// Whether <paramref name="normalisedExePath"/> is in the latest snapshot — by its image path, or, for a process
    /// whose path could not be read, by its file name (a process we cannot identify must not end a hold early). Null
    /// when there is no snapshot yet.
    /// </summary>
    public bool? Contains(string normalisedExePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalisedExePath);
        IReadOnlyList<ProcessSnapshot>? snapshot = Current;
        if (snapshot is null)
        {
            return null;
        }

        string name = Path.GetFileName(normalisedExePath);
        foreach (ProcessSnapshot p in snapshot)
        {
            if (p.ImagePath is { } image
                ? string.Equals(image, normalisedExePath, StringComparison.OrdinalIgnoreCase)
                : string.Equals(p.ImageName, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
