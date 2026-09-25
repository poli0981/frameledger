namespace FrameLedger.Domain.AntiCheat;

/// <summary>
/// <c>games.hook_blocked_reason</c> read back: the guard reason that blocked the game, the anti-cheat family it named and
/// the signal it named it by — the Agent's <c>Reason|Family|Signal</c> (<c>SqliteGameConsentStore.BlockText</c>, since
/// 2026-09-25).
/// </summary>
/// <remarks>
/// A row written before 2026-09-25 says <c>"Reason: Family Signal"</c>, which cannot be split back apart (family names and
/// file names both carry spaces), and a reason this build does not know is not one it can reason about: both parse to
/// null, and D33's exception is never offered over a block nobody can read.
/// </remarks>
public readonly record struct StoredBlock(AntiCheatRefusalReason Reason, string Family, string Signal)
{
    /// <summary>The stored text as the three slots, or null when it is not in that shape.</summary>
    public static StoredBlock? Parse(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return null;
        }

        string[] parts = stored.Split('|', 3);
        if (parts.Length != 3)
        {
            return null;
        }

        // Declared NAMES only, exactly cased: Enum.TryParse alone also accepts numbers and values nobody declared.
        return Enum.TryParse(parts[0], ignoreCase: false, out AntiCheatRefusalReason reason)
               && Enum.IsDefined(reason)
               && string.Equals(reason.ToString(), parts[0], StringComparison.Ordinal)
            ? new StoredBlock(reason, parts[1], parts[2])
            : null;
    }

    /// <summary>
    /// D33 (owner decision 2026-09-26): a block of the kind a user-mode exception may be ASKED about — a module, a file or
    /// a folder, naming one family. Never a title list (<c>BlockedExecutable</c>, <c>BlockedStoreId</c>: the notorious
    /// titles are there by design). Whether the family is user-mode throughout, and whether nothing else is found, is the
    /// guard's answer to a tolerant pre-scan, never this type's (<c>19_SAFETY</c> §The user-mode exception).
    /// </summary>
    public bool IsExceptionable => Family.Length > 0
        && Reason is AntiCheatRefusalReason.BlockedModule or AntiCheatRefusalReason.AntiCheatFile or AntiCheatRefusalReason.AntiCheatDirectory;
}
