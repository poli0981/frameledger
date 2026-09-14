namespace FrameLedger.Shared.Safety;

/// <summary>
/// The version of FR-2.1's consent disclosure this build carries — the <c>Safety_Consent_*</c> strings in
/// <c>Strings.resx</c> beside this file (HANDOFF §P3 decision D14). Both processes reference the same constant:
/// the App shows the dialog and sends the version it showed; the Agent's <c>HelloAck</c> carries its own and
/// <c>SetHookEnabled</c> stamps only when the two agree — a mismatch means one side was updated without the
/// other, and the App says "restart both" rather than showing text the Agent would not stand behind
/// (<c>07_IPC</c> §The pipe is not a trust boundary; the ring handshake's rule one layer up, §S23-1).
/// </summary>
/// <remarks>
/// Bump the version whenever a <c>Safety_Consent_*</c> string changes MEANING in any language. A stamp carries
/// the version it was made against (<c>games.hook_consent_disclosure_version</c>), which is what lets a later
/// build tell consent given under old wording from consent given under the current one.
/// </remarks>
public static class SafetyDisclosure
{
    public const string Version = "consent-dialog/1";
}
