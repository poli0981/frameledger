namespace FrameLedger.Shared.Safety;

/// <summary>
/// The version of the guard-bypass disclosure (owner decision 2026-09-21, <c>19_SAFETY</c> §The user's bypass): the text
/// the App shows before a user may overrule the anti-cheat guard for one game, and the value the Agent stamps on the
/// row. Beside <see cref="SafetyDisclosure"/> and for the same reason: the two processes must be talking about the same
/// words, so the Agent refuses an acknowledgement of any other version.
/// </summary>
/// <remarks>
/// Bump it whenever the dialog's meaning changes (<c>Strings.resx</c> <c>Bypass_*</c>). A row stamped with an older
/// version keeps its bypass — the user did accept that text — and the gate does not compare versions; what the version
/// buys is the audit trail of WHICH warning was accepted.
/// </remarks>
public static class GuardBypassDisclosure
{
    // /2 (2026-09-21): the text now says what the bypass CANNOT do - a process an anti-cheat driver protects stays
    // closed to FrameLedger, and nothing will be built to open it. A row stamped /1 keeps its bypass: that text was accepted.
    public const string Version = "guard-bypass-dialog/2";

    /// <summary>
    /// What the user must type, exactly, before the dialog's primary button enables. Not localised on purpose: it is
    /// the same five letters in every language, so a screenshot or a bug report shows which act was performed.
    /// </summary>
    public const string ConfirmationPhrase = "BYPASS";
}
