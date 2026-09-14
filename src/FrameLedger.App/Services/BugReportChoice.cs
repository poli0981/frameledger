namespace FrameLedger.App.Services;

/// <summary>The user's choice on the bug report's preview.</summary>
public enum BugReportChoice
{
    /// <summary>Closed: the zip stays where it was written, nothing else happens.</summary>
    Close,

    /// <summary>Step 4: open the GitHub issue form (the zip is dragged in by hand).</summary>
    OpenIssue,

    /// <summary>Step 4's fallback: the environment summary as Markdown on the clipboard.</summary>
    CopyMarkdown,
}
