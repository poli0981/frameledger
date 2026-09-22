namespace FrameLedger.Application.Persistence;

/// <summary>
/// The one write that folds two library entries into one (2026-09-23, HANDOFF D26). The Agent's — its relocator is the
/// only caller — because it moves sessions, which are the Agent's rows (<c>06_DATA_MODEL</c> §Writer ownership).
/// </summary>
public interface IGameMerge
{
    /// <summary>
    /// Folds <see cref="GameMergePlan.DroppedId"/> into <see cref="GameMergePlan.SurvivorId"/> in one transaction: the
    /// dropped entry's sessions re-parented, any block or auto-disable it carried kept (hooking off), its crash count
    /// added, the row deleted, and the survivor at the target path. Null — nothing written — when either row is not what
    /// the plan saw: gone, removed from the library, at another path, or (for a survivor that moves) no longer holding the
    /// target's bytes. Otherwise the number of sessions that moved.
    /// </summary>
    ValueTask<int?> MergeAsync(GameMergePlan plan, CancellationToken ct = default);
}
