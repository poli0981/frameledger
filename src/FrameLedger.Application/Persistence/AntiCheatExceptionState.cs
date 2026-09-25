using FrameLedger.Domain.Consent;

namespace FrameLedger.Application.Persistence;

/// <summary>
/// D33 (owner decision 2026-09-26): <c>games.ac_exception_*</c> as read (schema 0014) — the Agent's eligibility answer
/// about a blocked game, the user's grant of its user-mode exception, and how the last grant ended. Read-only here: every
/// write is the consent store's (<c>IGameConsentStore</c>) or a downgrade in <c>ChangeExecutableAsync</c>.
/// </summary>
public sealed record AntiCheatExceptionState
{
    /// <summary>A row that was never looked at and never granted anything.</summary>
    public static AntiCheatExceptionState None { get; } = new();

    /// <summary>The sweep's answer: the game's exception may be granted now.</summary>
    public bool Eligible { get; init; }

    /// <summary>The guard's tolerant pre-scan behind <see cref="Eligible"/>, as <c>Reason|Family|Signal</c>; null when nothing was asked.</summary>
    public string? Verdict { get; init; }

    /// <summary>Successful Tier-1 sessions of the game when the sweep last counted.</summary>
    public int Sessions { get; init; }

    /// <summary>The rules version <see cref="Verdict"/> was reached under.</summary>
    public string? CheckedRulesVersion { get; init; }

    /// <summary>The executable's size when <see cref="Verdict"/> was reached.</summary>
    public long? CheckedExeSizeBytes { get; init; }

    /// <summary>The executable's mtime (unix ms) when <see cref="Verdict"/> was reached.</summary>
    public long? CheckedExeMtimeMs { get; init; }

    /// <summary>The block text <see cref="Verdict"/> was reached about.</summary>
    public string? CheckedBlock { get; init; }

    /// <summary>When the user granted the exception in force; null when none is.</summary>
    public DateTimeOffset? GrantedAt { get; init; }

    /// <summary>The family the grant covers — or covered, once it has ended.</summary>
    public string? Family { get; init; }

    /// <summary>The disclosure the grant was accepted under.</summary>
    public string? DisclosureVersion { get; init; }

    /// <summary>The size of the executable the grant was made on.</summary>
    public long? ExeSizeBytes { get; init; }

    /// <summary>The mtime (unix ms) of the executable the grant was made on.</summary>
    public long? ExeMtimeMs { get; init; }

    /// <summary>When the last grant ended.</summary>
    public DateTimeOffset? LapsedAt { get; init; }

    /// <summary>Why the last grant ended (<c>UserModeExceptionLapse</c>).</summary>
    public string? LapsedReason { get; init; }

    /// <summary>An exception is in force on the row (whether the option lets it apply is the capture path's question).</summary>
    public bool IsGranted => GrantedAt is not null && !string.IsNullOrEmpty(Family);

    /// <summary>Whether <see cref="Verdict"/> was reached about <paramref name="block"/>, under these rules, over these bytes.</summary>
    public bool IsCheckedFor(ExecutableFingerprint onDisk, string rulesVersion, string block) =>
        string.Equals(CheckedRulesVersion, rulesVersion, StringComparison.Ordinal)
        && string.Equals(CheckedBlock, block, StringComparison.Ordinal)
        && CheckedExeSizeBytes == onDisk.SizeBytes
        && CheckedExeMtimeMs == onDisk.MtimeUnixMs;

    /// <summary>Whether the grant in force was made on <paramref name="onDisk"/>'s bytes.</summary>
    public bool GrantCovers(ExecutableFingerprint onDisk) =>
        IsGranted && ExeSizeBytes == onDisk.SizeBytes && ExeMtimeMs == onDisk.MtimeUnixMs;
}
