using System.Globalization;
using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Services;

/// <summary>D33: how an ask for a user-mode exception ended, and — for a refusal — what the Agent named.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed record AntiCheatExceptionResult(AntiCheatExceptionOutcome Outcome, string? Detail = null, RefusedAck? Refusal = null)
{
    public static AntiCheatExceptionResult Of(AntiCheatExceptionOutcome outcome) => new(outcome);

    /// <summary>The outcome a user wanted: the exception made, or withdrawn.</summary>
    public bool Succeeded => Outcome is AntiCheatExceptionOutcome.Granted or AntiCheatExceptionOutcome.Withdrawn;

    /// <summary>What to tell the user about <paramref name="gameName"/>, or null when there is nothing to say (a declined disclosure).</summary>
    public string? MessageFor(string gameName) => Outcome switch
    {
        AntiCheatExceptionOutcome.Granted => string.Format(CultureInfo.CurrentCulture, Strings.Exception_Result_Granted_Format, gameName),
        AntiCheatExceptionOutcome.Withdrawn => string.Format(CultureInfo.CurrentCulture, Strings.Exception_Result_Withdrawn_Format, gameName),
        AntiCheatExceptionOutcome.Refused => string.Format(CultureInfo.CurrentCulture, Strings.Exception_Result_Refused_Format, gameName,
            RefusalText() ?? Detail ?? Strings.Common_NotAvailable),
        AntiCheatExceptionOutcome.VersionMismatch => Shared.Strings.Safety_Exception_VersionMismatch,
        AntiCheatExceptionOutcome.OptionOff => Shared.Strings.Safety_Exception_OptionOff,
        AntiCheatExceptionOutcome.AgentUnavailable => Shared.Strings.Safety_Consent_AgentUnavailable,
        AntiCheatExceptionOutcome.Failed => Detail ?? Strings.Common_NotAvailable,
        _ => null,
    };

    /// <summary>Why no exception was made, in the user's language: the session count, the finding, or the block's kind.</summary>
    public string? RefusalText()
    {
        if (Refusal is null)
        {
            return null;
        }

        return Refusal.Reason switch
        {
            "TooFewSessions" => string.Format(CultureInfo.CurrentCulture, Strings.Exception_State_TooFew_Format, Refusal.Signal ?? "0"),
            "BlockNotExceptionable" => Strings.Exception_State_NotAsked,
            "PreScanFailed" or "RulesUnreadable" or "RulesMalformed" or "RulesIncomplete" => Strings.Exception_State_CouldNotVerify,
            _ => string.Format(CultureInfo.CurrentCulture, Strings.Exception_State_Refused_Format,
                Refusal.Family ?? Formats.GuardReasonText(Refusal.Reason), Refusal.Signal ?? Strings.Common_NotAvailable),
        };
    }
}
