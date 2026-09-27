namespace FrameLedger.Shared.Safety;

/// <summary>
/// The version of the admin mode's disclosure this build carries (beta.10, owner decision D34) — the
/// <c>Safety_AdminMode_*</c> strings in <c>Strings.resx</c> beside this file, shown before <c>capture.run_elevated</c> is
/// turned on. The same discipline as <see cref="SafetyDisclosure"/>: bump it whenever a <c>Safety_AdminMode_*</c> string
/// changes MEANING in any language.
/// </summary>
/// <remarks>
/// Nothing records an acceptance of it: the option being on is the acceptance, turning it on always shows the text, and
/// Windows asks again at every start of the Agent — which is the consent that matters, and it is Windows' own.
/// </remarks>
public static class AgentAdminDisclosure
{
    public const string Version = "agent-admin-dialog/1";
}
