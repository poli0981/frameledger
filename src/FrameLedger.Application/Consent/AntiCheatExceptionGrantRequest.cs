using FrameLedger.Domain.AntiCheat;
using FrameLedger.Domain.Consent;

namespace FrameLedger.Application.Consent;

/// <summary>
/// D33 (owner decision 2026-09-26): what a grant of a user-mode exception rests on, gathered by the Agent at the moment the
/// user accepted the disclosure — never supplied by a client (<c>07_IPC</c> §The pipe is not a trust boundary).
/// </summary>
public sealed record AntiCheatExceptionGrantRequest
{
    /// <summary>The executable as it is on disk right now; the grant covers these bytes and no others.</summary>
    public required ExecutableFingerprint Fingerprint { get; init; }

    /// <summary>The row's <c>hook_blocked_reason</c> the facts below are about; the write happens only while the row still carries it.</summary>
    public required string Block { get; init; }

    /// <summary>The guard's tolerant pre-scan, asked about the block's family just now.</summary>
    public required AntiCheatVerdict Verdict { get; init; }

    /// <summary>Successful Tier-1 sessions of the game, counted just now.</summary>
    public required int Sessions { get; init; }

    /// <summary>The disclosure the user accepted — the Agent's own version, checked before this request exists.</summary>
    public required string DisclosureVersion { get; init; }

    /// <summary>The Agent's clock.</summary>
    public required DateTimeOffset GrantedAt { get; init; }
}
