using FrameLedger.Domain.Consent;

namespace FrameLedger.Application.Consent;

/// <summary>
/// The record of a human accepting the guard-bypass disclosure for one executable (owner decision 2026-09-21,
/// <c>19_SAFETY</c> §The user's bypass). The shape <see cref="OperatorAcknowledgement"/> has, for the same reason: what is
/// written is an acknowledgement of a named text at a time, never a flag somebody sets.
/// </summary>
public sealed record GuardBypassAcknowledgement
{
    public required ExecutableFingerprint Fingerprint { get; init; }

    /// <summary>The version of the bypass disclosure that was shown. Empty is refused: an acknowledgement of nothing is not one.</summary>
    public required string DisclosureVersion { get; init; }

    public required DateTimeOffset AcknowledgedAt { get; init; }
}
