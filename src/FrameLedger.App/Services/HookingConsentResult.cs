using FrameLedger.Shared.Ipc;

namespace FrameLedger.App.Services;

/// <summary>The outcome, and — for a refusal — what the Agent's pre-scan named.</summary>
public sealed record HookingConsentResult(HookingConsentOutcome Outcome, string? Detail = null, RefusedAck? Refusal = null)
{
    public static HookingConsentResult Of(HookingConsentOutcome outcome) => new(outcome);

    /// <summary>The user-facing sentence for a refusal (<c>Safety_Refused_*</c>): named signal, unnamed block, or "could not verify".</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format string is a resource that follows the UI culture, which changes at runtime; a cached CompositeFormat would pin the first culture")]
    public string? RefusalText()
    {
        if (Refusal is null)
        {
            return null;
        }

        if (string.Equals(Refusal.Reason, "PreScanCouldNotVerify", StringComparison.Ordinal))
        {
            return Shared.Strings.Safety_Refused_CouldNotVerify;
        }

        string? family = Refusal.Family ?? Refusal.Signal;
        return family is null
            ? Shared.Strings.Safety_Refused_Unnamed
            : string.Format(System.Globalization.CultureInfo.CurrentCulture, Shared.Strings.Safety_Refused_Named_Format, family, Refusal.Signal ?? Refusal.Reason);
    }
}
