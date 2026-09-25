namespace FrameLedger.Shared.Safety;

/// <summary>
/// D33 (owner decision 2026-09-26): the version of the user-mode anti-cheat exception's disclosure this build carries — the
/// <c>Safety_Exception_*</c> strings in <c>Strings.resx</c> beside this file. The same rule as
/// <see cref="SafetyDisclosure"/>: the App shows the text and sends the version it showed, the Agent's <c>HelloAck</c>
/// carries its own, and <c>SetAntiCheatException</c> grants only when the two agree.
/// </summary>
/// <remarks>
/// Bump it whenever a <c>Safety_Exception_*</c> string changes MEANING in any language. A grant carries the version it was
/// made against (<c>games.ac_exception_disclosure_version</c>, schema 0014).
/// </remarks>
public static class AntiCheatExceptionDisclosure
{
    public const string Version = "ac-exception-dialog/1";
}
