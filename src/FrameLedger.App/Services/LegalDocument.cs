namespace FrameLedger.App.Services;

/// <summary>One of FR-11's documents as this build ships it: its key (the <c>legal_acceptance.doc</c> value), the title shown, the version the acceptance row records, the text, and the GitHub-hosted copy.</summary>
public sealed record LegalDocument(string Key, string Title, string Version, string Text, Uri Url);
