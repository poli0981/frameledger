namespace FrameLedger.Application.Consent;

/// <summary>
/// Writes the per-game guard bypass (owner decision 2026-09-21). Reads travel with the consent record
/// (<c>GameConsentRecord.GuardBypassAcknowledged</c>), because the gate reads consent and the bypass in one look at one
/// row; the writes are their own port so that nothing which can grant consent can also, by the same call, overrule the
/// guard.
/// </summary>
/// <remarks>
/// <para>
/// <b>It can only be recorded for a row that exists and for the executable that row is about.</b> A game nobody added
/// has nothing to bypass, and an acknowledgement for a binary other than the stored one is
/// <see cref="ConsentWriteOutcome.StaleFingerprint"/>, never a re-point.
/// </para>
/// <para>
/// It writes two columns and no others: not <c>hook_enabled</c>, not the consent stamp, not
/// <c>hook_blocked_reason</c>. A block stays on the row and stays true; the gate is what reads the bypass beside it.
/// There is no global form of this and no method here that could produce one.
/// </para>
/// </remarks>
public interface IGuardBypassStore
{
    ValueTask<ConsentWriteOutcome> RecordAsync(GuardBypassAcknowledgement acknowledgement, CancellationToken ct = default);

    /// <summary>Turns the bypass off for the row. Idempotent: a row without one is <see cref="ConsentWriteOutcome.Written"/> too.</summary>
    ValueTask<ConsentWriteOutcome> RevokeAsync(string normalisedExePath, CancellationToken ct = default);
}
