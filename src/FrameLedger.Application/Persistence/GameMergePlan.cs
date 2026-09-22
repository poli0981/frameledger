using FrameLedger.Domain.Consent;

namespace FrameLedger.Application.Persistence;

/// <summary>
/// Two library entries for one executable under two drive letters, and which one stays (2026-09-23, HANDOFF D26). The
/// owner's library showed <c>H:</c> twins beside their <c>D:</c> entries for a day: a drive that changed its letter made
/// a second entry for the same file, and once the letter changed back neither could move onto the other's path.
/// </summary>
/// <remarks>
/// <b>The entry made first stays</b> (a tie keeps the lower id): its name, notes, consent and history are the user's
/// oldest, and the other entry's sessions join it. Nothing about consent moves from the dropped entry to the survivor —
/// a merge can only ever carry a block or an auto-disable, which turn hooking off.
/// </remarks>
public sealed record GameMergePlan
{
    public required long SurvivorId { get; init; }

    /// <summary>Where the survivor's row says its executable is, as the plan saw it.</summary>
    public required string SurvivorPath { get; init; }

    public required long DroppedId { get; init; }

    /// <summary>Where the dropped row says its executable is, as the plan saw it.</summary>
    public required string DroppedPath { get; init; }

    /// <summary>The one file both entries name: where the survivor lives after the merge, with the bytes it was found with.</summary>
    public required ExecutableFingerprint Target { get; init; }

    /// <summary>The survivor is the entry whose file is gone, so it takes the target's path once the other row is deleted.</summary>
    public bool SurvivorMoves => !string.Equals(SurvivorPath, Target.ExePath, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The plan for <paramref name="stale"/> — the entry whose file is gone — and <paramref name="owner"/>, the entry at
    /// <paramref name="target"/>'s path.
    /// </summary>
    public static GameMergePlan For(GameRow stale, GameRow owner, ExecutableFingerprint target)
    {
        ArgumentNullException.ThrowIfNull(stale);
        ArgumentNullException.ThrowIfNull(owner);
        if (stale.Id == owner.Id)
        {
            throw new ArgumentException("an entry is not its own twin", nameof(owner));
        }

        if (!string.Equals(owner.Fingerprint.ExePath, target.ExePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("the owner is the entry at the target's path", nameof(owner));
        }

        bool staleStays = stale.AddedAt < owner.AddedAt || (stale.AddedAt == owner.AddedAt && stale.Id < owner.Id);
        GameRow keep = staleStays ? stale : owner;
        GameRow drop = staleStays ? owner : stale;
        return new GameMergePlan
        {
            SurvivorId = keep.Id,
            SurvivorPath = keep.Fingerprint.ExePath,
            DroppedId = drop.Id,
            DroppedPath = drop.Fingerprint.ExePath,
            Target = target,
        };
    }
}
