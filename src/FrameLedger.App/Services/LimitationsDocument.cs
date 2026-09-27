using System.IO;

namespace FrameLedger.App.Services;

/// <summary>
/// <c>LIMITATIONS.md</c> (beta.10, owner decision D36): what the whole software cannot do, in the user's terms — embedded at
/// build time from the repository root and shown under Help ▸ Limitations, so the App shows exactly the text this build
/// was reviewed with.
/// </summary>
/// <remarks>
/// <b>Information, not terms.</b> It is NOT one of FR-11's documents (<see cref="LegalDocuments.Keys"/>): the Legal Gate
/// records an acceptance per document it shows, and nothing here is accepted. The Disclaimer points at it instead.
/// </remarks>
public static class LimitationsDocument
{
    /// <summary>The embedded resource's name (<c>FrameLedger.App.csproj</c>).</summary>
    public const string ResourceName = "LIMITATIONS.md";

    /// <summary>The same text in the repository, for the dialog's button.</summary>
    public static readonly Uri OnGitHub = new(IssueLink.Repository + "/blob/main/LIMITATIONS.md");

    /// <summary>The document's text, authoring comments removed.</summary>
    /// <exception cref="InvalidOperationException">The build did not embed it.</exception>
    public static string Load()
    {
        using Stream stream = typeof(LimitationsDocument).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"the build did not embed {ResourceName}");
        using var reader = new StreamReader(stream);
        return LegalDocuments.StripComments(reader.ReadToEnd());
    }
}
