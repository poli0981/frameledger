using System.Globalization;

namespace FrameLedger.App.Services;

/// <summary>What a <c>DeleteSessions</c> ask did (beta.11, D40), and the sentence to tell the user.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "the format strings are resources that follow the UI culture, which changes at runtime")]
public sealed record SessionDeletionResult(SessionDeletionOutcome Outcome, int Games = 0, int Sessions = 0, string? Detail = null)
{
    public bool Succeeded => Outcome == SessionDeletionOutcome.Deleted;

    public string Message => Outcome switch
    {
        SessionDeletionOutcome.Deleted => string.Format(CultureInfo.CurrentCulture, Strings.DeleteSessions_Done_Format, Sessions, Games),
        SessionDeletionOutcome.SessionRunning => Strings.DeleteSessions_Running,
        SessionDeletionOutcome.AgentUnavailable => Strings.DeleteSessions_NoAgent,
        SessionDeletionOutcome.AgentTooOld => Strings.DeleteSessions_AgentTooOld,
        _ => Detail ?? Strings.Common_NotAvailable,
    };
}
