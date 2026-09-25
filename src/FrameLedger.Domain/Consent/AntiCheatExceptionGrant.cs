namespace FrameLedger.Domain.Consent;

/// <summary>
/// D33 (owner decision 2026-09-26): the user's grant of a user-mode anti-cheat exception for one game, as stored —
/// <c>games.ac_exception_*</c> (schema 0014). It names the family the user accepted the risk for, the disclosure they were
/// shown, and the executable (size and mtime) the grant was made on.
/// </summary>
/// <remarks>
/// <para>
/// <b>A grant is not a pass.</b> It is one input to <c>HookRequest.FromConsent</c>, which asks the guard to tolerate the
/// family only when the Settings option is on, the block on the row names that same family and the executable on disk is
/// the one the grant was made on; the guard then decides itself whether the family is user-mode and runs every check
/// (<c>19_SAFETY</c> §The user-mode exception).
/// </para>
/// <para>
/// It is only ever read out of a <see cref="GameConsentRecord"/>, which only a store can produce
/// (<see cref="GameConsentRecord.Stored"/> is internal) — the same reason a consent stamp cannot be minted.
/// </para>
/// </remarks>
public readonly record struct AntiCheatExceptionGrant
{
    /// <summary>The anti-cheat family the exception covers, as the guard names it.</summary>
    public required string Family { get; init; }

    /// <summary>When the user granted it, UTC.</summary>
    public required DateTimeOffset GrantedAt { get; init; }

    /// <summary>Which wording of the exception's disclosure the user accepted (<c>AntiCheatExceptionDisclosure.Version</c>).</summary>
    public required string DisclosureVersion { get; init; }

    /// <summary>The size of the executable the grant was made on.</summary>
    public required long ExeSizeBytes { get; init; }

    /// <summary>The mtime (unix ms) of the executable the grant was made on.</summary>
    public required long ExeMtimeUnixMs { get; init; }

    /// <summary>
    /// Whether <paramref name="observed"/> is the executable the grant was made on. Size and mtime only: a drive that changed
    /// its letter moves the path and keeps the bytes, and a game update changes the bytes and ends the grant.
    /// </summary>
    public bool Covers(ExecutableFingerprint observed) =>
        observed.SizeBytes == ExeSizeBytes && observed.MtimeUnixMs == ExeMtimeUnixMs;
}
