namespace FrameLedger.App.Update;

/// <summary>A release newer than the installed one, as the feed describes it: the version, its notes (the GitHub release body, Markdown), the full package's size.</summary>
public sealed record UpdateCandidate(string Version, string? NotesMarkdown, long SizeBytes);
