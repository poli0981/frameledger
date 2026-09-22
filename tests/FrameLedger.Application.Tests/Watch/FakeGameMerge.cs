using FrameLedger.Application.Persistence;
using FrameLedger.Application.Tests.Recording;

namespace FrameLedger.Application.Tests.Watch;

/// <summary>
/// An <see cref="IGameMerge"/> over <see cref="FakeGameRepository"/>'s rows (2026-09-23): the dropped row goes, the survivor
/// stands at the target with any block carried. The SQL — sessions, the order the schema needs — has its own tests
/// (<c>SqliteGameMergeTests</c>); this is what the relocator's callers see.
/// </summary>
internal sealed class FakeGameMerge(FakeGameRepository games) : IGameMerge
{
    public List<GameMergePlan> Plans { get; } = [];

    /// <summary>When set, every merge answers "a row changed underneath" and writes nothing.</summary>
    public bool Refuse { get; set; }

    public ValueTask<int?> MergeAsync(GameMergePlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Plans.Add(plan);
        if (Refuse)
        {
            return ValueTask.FromResult<int?>(null);
        }

        GameRow keep = games.Rows.Values.Single(r => r.Id == plan.SurvivorId);
        GameRow drop = games.Rows.Values.Single(r => r.Id == plan.DroppedId);
        games.Rows.Remove(drop.Fingerprint.ExePath);
        games.Rows.Remove(keep.Fingerprint.ExePath);
        games.Rows[plan.Target.ExePath] = keep with
        {
            Fingerprint = plan.SurvivorMoves ? plan.Target : keep.Fingerprint,
            HookBlockedReason = keep.HookBlockedReason ?? drop.HookBlockedReason,
            HookEnabled = keep.HookEnabled && keep.HookBlockedReason is null && drop.HookBlockedReason is null,
            HookCrashCount = keep.HookCrashCount + drop.HookCrashCount,
        };
        return ValueTask.FromResult<int?>(0);
    }
}
