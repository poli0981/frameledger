namespace FrameLedger.App.Services;

/// <summary>What step 3's preview shows (<c>10_LOGGING</c> §Bug report flow): the zip, every entry it holds, and the two ways forward.</summary>
public sealed record BugReportPreviewModel(string ZipPath, IReadOnlyList<string> Entries, Uri IssueUrl, string Markdown);
